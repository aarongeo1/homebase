using System;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Homebase.Data.Repositories;
using Homebase.Domain.Automations;
using Homebase.Domain.Devices;
using Homebase.Testing;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Homebase.Data.Tests
{
    public class RepositoryTests : IDisposable
    {
        private static readonly DateTime Now = new DateTime(2026, 10, 26, 23, 12, 5, 250, DateTimeKind.Utc);
        private readonly SqliteDatabase _db = new SqliteDatabase();

        public void Dispose() => _db.Dispose();

        [Fact]
        public void Migrations_RunOnce()
        {
            Assert.Empty(MigrationRunner.Run(_db.Connections));
        }

        [Fact]
        public async Task Seed_MatchesTheTestHousehold()
        {
            // TestHome claims to mirror 0002_seed_household.sql; this keeps the two honest.
            var expected = TestHome.Create(Now);
            var automations = await new SqliteAutomationRepository(_db.Connections).ListAsync();
            var scenes = await new SqliteSceneRepository(_db.Connections).ListAsync();
            var rooms = await new SqliteRoomRepository(_db.Connections).ListAsync();

            Assert.Equal(expected.Rooms.Select(r => r.Id + "|" + r.Name), rooms.Select(r => r.Id + "|" + r.Name));
            Assert.Equal(Shape(expected.Scenes), Shape(scenes));
            Assert.Equal(expected.Automations.OrderBy(a => a.Name, StringComparer.Ordinal).Select(Shape), automations.Select(Shape));
        }

        [Fact]
        public async Task Devices_RoundTripAndSortRoomlessLast()
        {
            var devices = new SqliteDeviceRepository(_db.Connections);
            await devices.AddAsync(new Device
            {
                Id = "light-a",
                Name = "Desk lamp",
                Type = DeviceType.Light,
                Driver = DriverKeys.GoveeLan,
                DriverAddress = "192.168.1.31",
                HardwareId = "AA:01",
                Online = true,
                LastChangedAt = Now,
                State = new DeviceState { On = true, Brightness = 40, ColorTempK = 2700 },
            });

            var light = await devices.GetByHardwareIdAsync("AA:01");
            Assert.Equal("light-a", light.Id);
            Assert.True(light.State.On);
            Assert.Equal(2700, light.State.ColorTempK);
            Assert.Equal(DateTimeKind.Utc, light.LastChangedAt.Kind);
            Assert.Equal(Now, light.LastChangedAt);
            Assert.Equal(new[] { "tv", "light-a", "phone" }, (await devices.ListAsync()).Select(d => d.Id)); // living room, then no room by name

            light.RoomId = "bedroom";
            light.Name = "Bedside lamp";
            await devices.SaveDefinitionAsync(light);
            light.State = new DeviceState { On = false, Brightness = 40 };
            light.Online = false;
            await devices.SaveStateAsync(light);

            var reloaded = await devices.GetAsync("light-a");
            Assert.Equal("bedroom", reloaded.RoomId);
            Assert.Equal("Bedside lamp", reloaded.Name);
            Assert.False(reloaded.Online);
            Assert.False(reloaded.State.On);
            Assert.Null(reloaded.State.ColorTempK);
        }

        [Fact]
        public async Task Constraints_RejectDuplicatesAndBadValues()
        {
            var devices = new SqliteDeviceRepository(_db.Connections);
            var light = new Device { Id = "light-a", Name = "A", Type = DeviceType.Light, Driver = DriverKeys.GoveeLan, DriverAddress = "1.2.3.4", HardwareId = "AA:01", Online = true, LastChangedAt = Now };
            await devices.AddAsync(light);
            light.Id = "light-b";
            await Assert.ThrowsAsync<SqliteException>(() => devices.AddAsync(light));

            var bad = new Device { Id = "light-c", Name = "C", Type = DeviceType.Light, Driver = DriverKeys.Simulated, Online = true, LastChangedAt = Now, State = new DeviceState { Brightness = 140 } };
            await Assert.ThrowsAsync<SqliteException>(() => devices.AddAsync(bad));

            var noAddress = new Device { Id = "light-d", Name = "D", Type = DeviceType.Light, Driver = DriverKeys.GoveeLan, Online = true, LastChangedAt = Now };
            await Assert.ThrowsAsync<SqliteException>(() => devices.AddAsync(noAddress));

            light.Id = "light-e";
            light.HardwareId = "AA:02";
            light.RoomId = "garage";
            await Assert.ThrowsAsync<SqliteException>(() => devices.AddAsync(light)); // foreign keys are on
        }

        [Fact]
        public async Task Runs_KeepTheirTraceAndFeedLastRun()
        {
            var automations = new SqliteAutomationRepository(_db.Connections);
            await automations.AddRunAsync(new AutomationRun
            {
                Id = "run_1",
                AutomationId = "charging-at-night",
                FiredAt = Now,
                Outcome = RunOutcome.Cancelled,
                Trace = { new TraceStep("Trigger: When Phone has been charging for 15 min", false, "Phone stopped being charging after 6 of 15 min.") },
            });

            var run = (await automations.ListRunsAsync(10)).Single();
            Assert.Equal("Phone charging at night → bedroom off", run.AutomationName);
            Assert.Equal(RunOutcome.Cancelled, run.Outcome);
            Assert.Equal("Phone stopped being charging after 6 of 15 min.", run.Trace.Single().Detail);
            Assert.False(run.Trace.Single().Passed);

            var automation = await automations.GetAsync("charging-at-night");
            Assert.Equal(Now, automation.LastRunAt);
            Assert.Equal(RunOutcome.Cancelled, automation.LastOutcome);
        }

        [Fact]
        public async Task SetEnabled_AndSettingsUpsert()
        {
            var automations = new SqliteAutomationRepository(_db.Connections);
            await automations.SetEnabledAsync("woke-up", false);
            Assert.False((await automations.GetAsync("woke-up")).Enabled);

            var settings = new SqliteSettingsRepository(_db.Connections);
            Assert.Null(await settings.GetAsync("androidtv.client-certificate"));
            await settings.SetAsync("androidtv.client-certificate", "one");
            await settings.SetAsync("androidtv.client-certificate", "two");
            Assert.Equal("two", await settings.GetAsync("androidtv.client-certificate"));
        }

        [Fact]
        public async Task HealthCheck_Connects()
        {
            Assert.True(await new SqliteDatabaseHealthCheck(_db.Connections).CanConnectAsync());
            using (var connection = _db.Connections.Create())
            {
                Assert.Equal(1L, await connection.ExecuteScalarAsync<long>("PRAGMA foreign_keys;"));
            }
        }

        private static string Shape(Automation a) =>
            a.Id + "|" + a.Name + "|" + a.Enabled + "|" + a.Trigger.Kind + "|" + a.Trigger.DeviceId + "|" + a.Trigger.Field + "|" + a.Trigger.EqualsValue + "|" + a.Trigger.ForMinutes
            + "|" + string.Join(";", a.Conditions.Select(c => c.Kind + ":" + c.After + "-" + c.Before + ":" + c.EqualsValue))
            + "|" + string.Join(";", a.Actions.Select(Shape));

        private static string[] Shape(System.Collections.Generic.IEnumerable<Scene> scenes) =>
            scenes.OrderBy(s => s.Id, StringComparer.Ordinal).Select(s => s.Id + "|" + s.Name + "|" + string.Join(";", s.Actions.Select(Shape))).ToArray();

        private static string Shape(HomeAction a) =>
            a.Kind + ":" + a.DeviceId + a.RoomId + a.SceneId + ":" + (a.Command == null ? "-" : a.Command.On + "/" + a.Command.Brightness + "/" + a.Command.ColorTempK);
    }
}

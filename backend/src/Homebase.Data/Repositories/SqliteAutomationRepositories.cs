using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using Homebase.Domain.Abstractions;
using Homebase.Domain.Automations;
using Homebase.Domain.Devices;

namespace Homebase.Data.Repositories
{
    public sealed class SqliteSceneRepository : ISceneRepository
    {
        private readonly IDbConnectionFactory _connections;

        public SqliteSceneRepository(IDbConnectionFactory connections)
        {
            _connections = connections;
        }

        public Task<IReadOnlyList<Scene>> ListAsync() => LoadAsync(null);

        public async Task<Scene> GetAsync(string id) => (await LoadAsync(id).ConfigureAwait(false)).FirstOrDefault();

        private async Task<IReadOnlyList<Scene>> LoadAsync(string id)
        {
            using (var connection = _connections.Create())
            {
                var filter = id == null ? string.Empty : " WHERE Id = @Id";
                var scenes = (await connection.QueryAsync<SceneRow>("SELECT Id, Name, Description FROM Scenes" + filter + " ORDER BY Name;", new { Id = id }).ConfigureAwait(false)).ToList();
                var actions = await ActionRows.LoadAsync(connection, "SceneActions", "SceneId", id).ConfigureAwait(false);
                return scenes.Select(s => new Scene
                {
                    Id = s.Id,
                    Name = s.Name,
                    Description = s.Description,
                    Actions = actions.Where(a => a.OwnerId == s.Id).Select(ActionRows.Map).ToList(),
                }).ToList();
            }
        }

        private sealed class SceneRow
        {
            public string Id { get; set; }

            public string Name { get; set; }

            public string Description { get; set; }
        }
    }

    public sealed class SqliteAutomationRepository : IAutomationRepository
    {
        /// <summary>Run history is trimmed to the newest runs so the file never grows without bound.</summary>
        public const int KeepRuns = 2000;

        private const string SelectAutomations = @"
SELECT a.Id, a.Name, a.Description, a.Enabled, a.TriggerKind, a.TriggerDeviceId, a.TriggerField, a.TriggerEquals, a.TriggerForMinutes,
       a.TriggerAt, a.TriggerDays,
       (SELECT r.FiredAt FROM AutomationRuns r WHERE r.AutomationId = a.Id ORDER BY r.FiredAt DESC LIMIT 1) AS LastRunAt,
       (SELECT r.Outcome FROM AutomationRuns r WHERE r.AutomationId = a.Id ORDER BY r.FiredAt DESC LIMIT 1) AS LastOutcome
FROM Automations a";

        private readonly IDbConnectionFactory _connections;

        public SqliteAutomationRepository(IDbConnectionFactory connections)
        {
            _connections = connections;
        }

        public Task<IReadOnlyList<Automation>> ListAsync() => LoadAsync(null);

        public async Task<Automation> GetAsync(string id) => (await LoadAsync(id).ConfigureAwait(false)).FirstOrDefault();

        public async Task SetEnabledAsync(string id, bool enabled)
        {
            using (var connection = _connections.Create())
            {
                await connection.ExecuteAsync("UPDATE Automations SET Enabled = @Enabled WHERE Id = @Id;", new { Id = id, Enabled = enabled }).ConfigureAwait(false);
            }
        }

        public async Task AddRunAsync(AutomationRun run)
        {
            var trace = JsonSerializer.Serialize(run.Trace.Select(t => new TraceRow { Label = t.Label, Passed = t.Passed, Detail = t.Detail }));
            using (var connection = _connections.Create())
            {
                await connection.ExecuteAsync(
                    "INSERT INTO AutomationRuns (Id, AutomationId, FiredAt, Outcome, TraceJson, ActionsExecuted) VALUES (@Id, @AutomationId, @FiredAt, @Outcome, @TraceJson, @ActionsExecuted);",
                    new
                    {
                        run.Id,
                        run.AutomationId,
                        FiredAt = Sql.Timestamp(run.FiredAt),
                        Outcome = DbCodes.Outcome.Code(run.Outcome),
                        TraceJson = trace,
                        run.ActionsExecuted,
                    }).ConfigureAwait(false);
                await connection.ExecuteAsync(
                    "DELETE FROM AutomationRuns WHERE Id NOT IN (SELECT Id FROM AutomationRuns ORDER BY FiredAt DESC LIMIT @Keep);",
                    new { Keep = KeepRuns }).ConfigureAwait(false);
            }
        }

        public async Task<IReadOnlyList<AutomationRun>> ListRunsAsync(int limit)
        {
            using (var connection = _connections.Create())
            {
                var rows = await connection.QueryAsync<RunRow>(@"
SELECT r.Id, r.AutomationId, a.Name AS AutomationName, r.FiredAt, r.Outcome, r.TraceJson, r.ActionsExecuted
FROM AutomationRuns r
JOIN Automations a ON a.Id = r.AutomationId
ORDER BY r.FiredAt DESC
LIMIT @Limit;", new { Limit = limit }).ConfigureAwait(false);

                return rows.Select(r => new AutomationRun
                {
                    Id = r.Id,
                    AutomationId = r.AutomationId,
                    AutomationName = r.AutomationName,
                    FiredAt = Sql.ParseTimestamp(r.FiredAt),
                    Outcome = DbCodes.Read(DbCodes.Outcome, r.Outcome),
                    ActionsExecuted = (int)r.ActionsExecuted,
                    Trace = (JsonSerializer.Deserialize<List<TraceRow>>(r.TraceJson) ?? new List<TraceRow>())
                        .Select(t => new TraceStep(t.Label, t.Passed, t.Detail))
                        .ToList(),
                }).ToList();
            }
        }

        private async Task<IReadOnlyList<Automation>> LoadAsync(string id)
        {
            using (var connection = _connections.Create())
            {
                var filter = id == null ? string.Empty : " WHERE a.Id = @Id";
                var rows = (await connection.QueryAsync<AutomationRow>(SelectAutomations + filter + " ORDER BY a.Name;", new { Id = id }).ConfigureAwait(false)).ToList();
                var conditionFilter = id == null ? string.Empty : " WHERE AutomationId = @Id";
                var conditions = (await connection.QueryAsync<ConditionRow>(
                    "SELECT AutomationId, Position, Kind, WindowAfter, WindowBefore, DeviceId, Field, EqualsValue FROM AutomationConditions"
                        + conditionFilter + " ORDER BY AutomationId, Position;",
                    new { Id = id }).ConfigureAwait(false)).ToList();
                var actions = await ActionRows.LoadAsync(connection, "AutomationActions", "AutomationId", id).ConfigureAwait(false);

                return rows.Select(r => new Automation
                {
                    Id = r.Id,
                    Name = r.Name,
                    Description = r.Description,
                    Enabled = r.Enabled != 0,
                    Trigger = new Trigger
                    {
                        Kind = DbCodes.Read(DbCodes.TriggerKind, r.TriggerKind),
                        DeviceId = r.TriggerDeviceId,
                        Field = DbCodes.ReadOrNull(DbCodes.Field, r.TriggerField),
                        EqualsValue = Sql.Bool(r.TriggerEquals),
                        ForMinutes = Sql.Int(r.TriggerForMinutes),
                        At = r.TriggerAt,
                        Days = r.TriggerDays == null ? null : r.TriggerDays.Split(',').Select(d => DbCodes.Read(DbCodes.Weekday, d)).ToList(),
                    },
                    Conditions = conditions.Where(c => c.AutomationId == r.Id).Select(c => new Condition
                    {
                        Kind = DbCodes.Read(DbCodes.ConditionKind, c.Kind),
                        After = c.WindowAfter,
                        Before = c.WindowBefore,
                        DeviceId = c.DeviceId,
                        Field = DbCodes.ReadOrNull(DbCodes.Field, c.Field),
                        EqualsValue = Sql.Bool(c.EqualsValue),
                    }).ToList(),
                    Actions = actions.Where(a => a.OwnerId == r.Id).Select(ActionRows.Map).ToList(),
                    LastRunAt = r.LastRunAt == null ? (DateTime?)null : Sql.ParseTimestamp(r.LastRunAt),
                    LastOutcome = DbCodes.ReadOrNull(DbCodes.Outcome, r.LastOutcome),
                }).ToList();
            }
        }

        private sealed class AutomationRow
        {
            public string Id { get; set; }

            public string Name { get; set; }

            public string Description { get; set; }

            public long Enabled { get; set; }

            public string TriggerKind { get; set; }

            public string TriggerDeviceId { get; set; }

            public string TriggerField { get; set; }

            public long? TriggerEquals { get; set; }

            public long? TriggerForMinutes { get; set; }

            public string TriggerAt { get; set; }

            public string TriggerDays { get; set; }

            public string LastRunAt { get; set; }

            public string LastOutcome { get; set; }
        }

        private sealed class ConditionRow
        {
            public string AutomationId { get; set; }

            public long Position { get; set; }

            public string Kind { get; set; }

            public string WindowAfter { get; set; }

            public string WindowBefore { get; set; }

            public string DeviceId { get; set; }

            public string Field { get; set; }

            public long? EqualsValue { get; set; }
        }

        private sealed class RunRow
        {
            public string Id { get; set; }

            public string AutomationId { get; set; }

            public string AutomationName { get; set; }

            public string FiredAt { get; set; }

            public string Outcome { get; set; }

            public string TraceJson { get; set; }

            public long ActionsExecuted { get; set; }
        }

        private sealed class TraceRow
        {
            [System.Text.Json.Serialization.JsonPropertyName("label")]
            public string Label { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("passed")]
            public bool Passed { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("detail")]
            public string Detail { get; set; }
        }
    }

    /// <summary>SceneActions and AutomationActions share one shape.</summary>
    internal static class ActionRows
    {
        public static async Task<List<ActionRow>> LoadAsync(DbConnection connection, string table, string ownerColumn, string ownerId)
        {
            var filter = ownerId == null ? string.Empty : " WHERE " + ownerColumn + " = @Id";
            var rows = await connection.QueryAsync<ActionRow>(
                "SELECT " + ownerColumn + " AS OwnerId, Position, Kind, DeviceId, RoomId, TargetSceneId, IsOn, Brightness, ColorTempK FROM " + table
                    + filter + " ORDER BY " + ownerColumn + ", Position;",
                new { Id = ownerId }).ConfigureAwait(false);
            return rows.ToList();
        }

        public static HomeAction Map(ActionRow row)
        {
            var kind = DbCodes.Read(DbCodes.ActionKind, row.Kind);
            return new HomeAction
            {
                Kind = kind,
                DeviceId = row.DeviceId,
                RoomId = row.RoomId,
                SceneId = row.TargetSceneId,
                Command = kind == ActionKind.Scene
                    ? null
                    : new DeviceCommand { On = Sql.Bool(row.IsOn), Brightness = Sql.Int(row.Brightness), ColorTempK = Sql.Int(row.ColorTempK) },
            };
        }

        internal sealed class ActionRow
        {
            public string OwnerId { get; set; }

            public long Position { get; set; }

            public string Kind { get; set; }

            public string DeviceId { get; set; }

            public string RoomId { get; set; }

            public string TargetSceneId { get; set; }

            public long? IsOn { get; set; }

            public long? Brightness { get; set; }

            public long? ColorTempK { get; set; }
        }
    }
}

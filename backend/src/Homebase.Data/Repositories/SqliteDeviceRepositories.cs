using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Homebase.Domain.Abstractions;
using Homebase.Domain.Devices;

namespace Homebase.Data.Repositories
{
    public sealed class SqliteRoomRepository : IRoomRepository
    {
        private readonly IDbConnectionFactory _connections;

        public SqliteRoomRepository(IDbConnectionFactory connections)
        {
            _connections = connections;
        }

        public async Task<IReadOnlyList<Room>> ListAsync()
        {
            using (var connection = _connections.Create())
            {
                var rows = await connection.QueryAsync<RoomRow>("SELECT Id, Name, SortOrder FROM Rooms ORDER BY SortOrder, Id;").ConfigureAwait(false);
                return rows.Select(r => new Room { Id = r.Id, Name = r.Name, SortOrder = (int)r.SortOrder }).ToList();
            }
        }

        private sealed class RoomRow
        {
            public string Id { get; set; }

            public string Name { get; set; }

            public long SortOrder { get; set; }
        }
    }

    public sealed class SqliteDeviceRepository : IDeviceRepository
    {
        // Devices without a room sort after every room.
        private const string SelectDevices = @"
SELECT d.Id, d.Name, d.RoomId, d.Type, d.Driver, d.DriverAddress, d.HardwareId, d.Online, d.IsOn, d.Brightness, d.ColorTempK,
       d.Home, d.Charging, d.Asleep, d.LastChangedAt
FROM Devices d
LEFT JOIN Rooms r ON r.Id = d.RoomId";

        private const string OrderDevices = " ORDER BY r.SortOrder IS NULL, r.SortOrder, d.Name;";

        private readonly IDbConnectionFactory _connections;

        public SqliteDeviceRepository(IDbConnectionFactory connections)
        {
            _connections = connections;
        }

        public async Task<IReadOnlyList<Device>> ListAsync()
        {
            using (var connection = _connections.Create())
            {
                var rows = await connection.QueryAsync<DeviceRow>(SelectDevices + OrderDevices).ConfigureAwait(false);
                return rows.Select(Map).ToList();
            }
        }

        public async Task<Device> GetAsync(string id)
        {
            using (var connection = _connections.Create())
            {
                var row = await connection.QuerySingleOrDefaultAsync<DeviceRow>(SelectDevices + " WHERE d.Id = @Id;", new { Id = id }).ConfigureAwait(false);
                return row == null ? null : Map(row);
            }
        }

        public async Task<Device> GetByHardwareIdAsync(string hardwareId)
        {
            using (var connection = _connections.Create())
            {
                var row = await connection.QuerySingleOrDefaultAsync<DeviceRow>(SelectDevices + " WHERE d.HardwareId = @HardwareId;", new { HardwareId = hardwareId }).ConfigureAwait(false);
                return row == null ? null : Map(row);
            }
        }

        public async Task AddAsync(Device device)
        {
            const string sql = @"
INSERT INTO Devices (Id, Name, RoomId, Type, Driver, DriverAddress, HardwareId, Online, IsOn, Brightness, ColorTempK, Home, Charging, Asleep, LastChangedAt)
VALUES (@Id, @Name, @RoomId, @Type, @Driver, @DriverAddress, @HardwareId, @Online, @IsOn, @Brightness, @ColorTempK, @Home, @Charging, @Asleep, @LastChangedAt);";

            var s = device.State;
            using (var connection = _connections.Create())
            {
                await connection.ExecuteAsync(sql, new
                {
                    device.Id,
                    device.Name,
                    device.RoomId,
                    Type = DbCodes.DeviceType.Code(device.Type),
                    device.Driver,
                    device.DriverAddress,
                    device.HardwareId,
                    device.Online,
                    IsOn = s.On,
                    s.Brightness,
                    s.ColorTempK,
                    s.Home,
                    s.Charging,
                    s.Asleep,
                    LastChangedAt = Sql.Timestamp(device.LastChangedAt),
                }).ConfigureAwait(false);
            }
        }

        public async Task SaveStateAsync(Device device)
        {
            const string sql = @"
UPDATE Devices
SET Online = @Online, IsOn = @IsOn, Brightness = @Brightness, ColorTempK = @ColorTempK, Home = @Home, Charging = @Charging,
    Asleep = @Asleep, LastChangedAt = @LastChangedAt
WHERE Id = @Id;";

            var s = device.State;
            await UpdateOneAsync(device.Id, sql, new
            {
                device.Id,
                device.Online,
                IsOn = s.On,
                s.Brightness,
                s.ColorTempK,
                s.Home,
                s.Charging,
                s.Asleep,
                LastChangedAt = Sql.Timestamp(device.LastChangedAt),
            }).ConfigureAwait(false);
        }

        public Task SaveDefinitionAsync(Device device)
        {
            const string sql = "UPDATE Devices SET Name = @Name, RoomId = @RoomId, Driver = @Driver, DriverAddress = @DriverAddress WHERE Id = @Id;";
            return UpdateOneAsync(device.Id, sql, new { device.Id, device.Name, device.RoomId, device.Driver, device.DriverAddress });
        }

        private async Task UpdateOneAsync(string id, string sql, object parameters)
        {
            using (var connection = _connections.Create())
            {
                if (await connection.ExecuteAsync(sql, parameters).ConfigureAwait(false) != 1)
                {
                    throw new InvalidOperationException("Device '" + id + "' was not updated.");
                }
            }
        }

        private static Device Map(DeviceRow row) => new Device
        {
            Id = row.Id,
            Name = row.Name,
            RoomId = row.RoomId,
            Type = DbCodes.Read(DbCodes.DeviceType, row.Type),
            Driver = row.Driver,
            DriverAddress = row.DriverAddress,
            HardwareId = row.HardwareId,
            Online = row.Online != 0,
            LastChangedAt = Sql.ParseTimestamp(row.LastChangedAt),
            State = new DeviceState
            {
                On = Sql.Bool(row.IsOn),
                Brightness = Sql.Int(row.Brightness),
                ColorTempK = Sql.Int(row.ColorTempK),
                Home = Sql.Bool(row.Home),
                Charging = Sql.Bool(row.Charging),
                Asleep = Sql.Bool(row.Asleep),
            },
        };

        private sealed class DeviceRow
        {
            public string Id { get; set; }

            public string Name { get; set; }

            public string RoomId { get; set; }

            public string Type { get; set; }

            public string Driver { get; set; }

            public string DriverAddress { get; set; }

            public string HardwareId { get; set; }

            public long Online { get; set; }

            public long? IsOn { get; set; }

            public long? Brightness { get; set; }

            public long? ColorTempK { get; set; }

            public long? Home { get; set; }

            public long? Charging { get; set; }

            public long? Asleep { get; set; }

            public string LastChangedAt { get; set; }
        }
    }

    public sealed class SqliteSettingsRepository : ISettingsRepository
    {
        private readonly IDbConnectionFactory _connections;

        public SqliteSettingsRepository(IDbConnectionFactory connections)
        {
            _connections = connections;
        }

        public async Task<string> GetAsync(string key)
        {
            using (var connection = _connections.Create())
            {
                return await connection.QuerySingleOrDefaultAsync<string>("SELECT Value FROM Settings WHERE Key = @Key;", new { Key = key }).ConfigureAwait(false);
            }
        }

        public async Task SetAsync(string key, string value)
        {
            using (var connection = _connections.Create())
            {
                await connection.ExecuteAsync(
                    "INSERT INTO Settings (Key, Value) VALUES (@Key, @Value) ON CONFLICT (Key) DO UPDATE SET Value = excluded.Value;",
                    new { Key = key, Value = value }).ConfigureAwait(false);
            }
        }
    }
}

using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace Homebase.Data.Tests
{
    /// <summary>A fresh, migrated database file per test; deleted afterwards.</summary>
    public sealed class SqliteDatabase : IDisposable
    {
        private readonly string _directory;

        public SqliteDatabase()
        {
            _directory = Path.Combine(Path.GetTempPath(), "homebase-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            Connections = new SqliteConnectionFactory(Path.Combine(_directory, "homebase.db"));
            MigrationRunner.Run(Connections);
        }

        public SqliteConnectionFactory Connections { get; }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(_directory, true);
        }
    }
}

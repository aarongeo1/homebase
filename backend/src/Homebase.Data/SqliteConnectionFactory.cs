using System;
using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using Homebase.Domain.Abstractions;
using Microsoft.Data.Sqlite;

namespace Homebase.Data
{
    public interface IDbConnectionFactory
    {
        /// <summary>Returns a new, closed connection. Callers own and dispose it.</summary>
        DbConnection Create();
    }

    /// <summary>One SQLite file (default ~/Library/Application Support/Homebase/homebase.db). Foreign keys are on for every connection.</summary>
    public sealed class SqliteConnectionFactory : IDbConnectionFactory
    {
        private readonly string _connectionString;

        public SqliteConnectionFactory(string databasePath)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
            {
                throw new ArgumentException("A database file path is required.", nameof(databasePath));
            }

            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                ForeignKeys = true,
                Pooling = true,
                DefaultTimeout = 10,
            }.ToString();
        }

        public DbConnection Create() => new SqliteConnection(_connectionString);
    }

    public sealed class SqliteDatabaseHealthCheck : IDatabaseHealthCheck
    {
        private readonly IDbConnectionFactory _connections;

        public SqliteDatabaseHealthCheck(IDbConnectionFactory connections)
        {
            _connections = connections;
        }

        public async Task<bool> CanConnectAsync()
        {
            try
            {
                using (var connection = _connections.Create())
                {
                    return await connection.ExecuteScalarAsync<long>("SELECT 1;").ConfigureAwait(false) == 1;
                }
            }
            catch (SqliteException)
            {
                return false;
            }
        }
    }
}

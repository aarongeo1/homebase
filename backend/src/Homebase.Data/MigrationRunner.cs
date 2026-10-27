using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dapper;

namespace Homebase.Data
{
    /// <summary>
    /// Applies database/migrations/*.sql (embedded in this assembly) in file-name order, one transaction per script,
    /// recording each in SchemaVersions so it never runs twice. Runs at every start; already-applied scripts are skipped,
    /// so never edit a shipped script — add a new numbered one.
    /// </summary>
    public static class MigrationRunner
    {
        private const string Prefix = "migrations.";

        /// <summary>Returns the names of the scripts applied by this call (empty when the database was up to date).</summary>
        public static IReadOnlyList<string> Run(IDbConnectionFactory connections)
        {
            var assembly = typeof(MigrationRunner).Assembly;
            var scripts = assembly.GetManifestResourceNames()
                .Where(n => n.StartsWith(Prefix, StringComparison.Ordinal) && n.EndsWith(".sql", StringComparison.Ordinal))
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();

            var applied = new List<string>();
            using (var connection = connections.Create())
            {
                connection.Open();
                connection.Execute("PRAGMA journal_mode = WAL;");
                connection.Execute("CREATE TABLE IF NOT EXISTS SchemaVersions (ScriptName TEXT NOT NULL PRIMARY KEY, AppliedAt TEXT NOT NULL);");
                var done = new HashSet<string>(connection.Query<string>("SELECT ScriptName FROM SchemaVersions;"), StringComparer.Ordinal);

                foreach (var resource in scripts)
                {
                    var name = resource.Substring(Prefix.Length);
                    if (done.Contains(name))
                    {
                        continue;
                    }

                    string sql;
                    using (var reader = new StreamReader(assembly.GetManifestResourceStream(resource)))
                    {
                        sql = reader.ReadToEnd();
                    }

                    using (var transaction = connection.BeginTransaction())
                    {
                        connection.Execute(sql, transaction: transaction);
                        connection.Execute(
                            "INSERT INTO SchemaVersions (ScriptName, AppliedAt) VALUES (@Name, @At);",
                            new { Name = name, At = Sql.Timestamp(DateTime.UtcNow) },
                            transaction);
                        transaction.Commit();
                    }

                    applied.Add(name);
                }
            }

            return applied;
        }
    }
}

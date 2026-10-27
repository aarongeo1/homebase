---
applyTo: "database/**/*.sql,backend/src/Homebase.Data/**"
description: SQLite schema, migration and data-access rules.
---
# Database (SQLite, embedded migrations, Dapper)

- **Schema changes are new files:** `database/migrations/NNNN_description.sql`, embedded in Homebase.Data and applied at startup by `MigrationRunner` (journal: `SchemaVersions`). Never edit a shipped script — the guardrail hook blocks edits to 0001/0002.
- One transaction per script. SQLite's `ALTER TABLE` cannot change a column or CHECK: create the new table, copy, drop the old one, rename — in one script — and test it against a copy of a real database file.
- **Types:** ids TEXT; booleans INTEGER with `CHECK (x IN (0, 1))`; timestamps TEXT `yyyy-MM-ddTHH:mm:ss.fffZ` UTC through `Sql.Timestamp` / `Sql.ParseTimestamp` (fixed width, so `ORDER BY` is chronological). Never store local time.
- **Row classes** use `long`/`long?` for INTEGER and `string` for TEXT; convert with `Sql.Bool` / `Sql.Int`. Don't rely on Dapper's type coercion.
- **Enums:** every enum column has a CHECK matching the contract. A new value touches four places: CHECK (new migration), `DbCodes`, `ApiCodes`, contract.
- Foreign keys are on for every connection (`SqliteConnectionFactory`); keep `REFERENCES` on every id column.
- Device state is typed nullable columns (`IsOn` avoids the keyword); fields a type lacks are NULL. `HardwareId` is unique when set — discovery depends on it.
- `Settings` holds driver secrets (the TV client certificate). Never return its values through the API.
- `TraceJson` has a `json_valid` check, shaped `[{label, passed, detail}]`. Run history is trimmed to the newest `KeepRuns`.
- **Queries:** parameterized (`@Name`), explicit columns, no `SELECT *`. Multi-statement writes in an explicit transaction.
- **The seed** (`0002_seed_household.sql`) must match `backend/tests/Homebase.Testing/TestHome.cs` (the `Seed_MatchesTheTestHousehold` test enforces it) and `frontend/src/shared/api/mock/fixtures.ts`.
- Data tests use the `SqliteDatabase` fixture (a fresh temp file per test); never the real data folder.

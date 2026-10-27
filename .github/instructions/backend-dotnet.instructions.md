---
applyTo: "backend/**/*.cs,backend/**/*.csproj,backend/**/*.props"
description: .NET 10 / ASP.NET Core / SQLite conventions for the Homebase backend.
---
# Backend (.NET 10, ASP.NET Core controllers, Dapper on SQLite)

- **House style:** block-scoped namespaces, `sealed` classes with get/set properties, explicit `out` declarations, no nullable annotations (`Nullable` is off). Newer C# compiles, but match the file you are in; don't modernize code in passing.
- **Warnings are errors.** Fix the cause; never `#pragma warning disable` / `NoWarn`. (Gotcha: a property named `Equals` hides `object.Equals`; use `EqualsValue` + `[JsonPropertyName("equals")]`.)
- **Layers:** `Homebase.Domain` pure rules, no dependencies · `Homebase.Devices` drivers + discovery · `Homebase.Application` orchestration (`HomeRuntime`, services) · `Homebase.Data` SQLite + migrations · `Homebase.Api` thin controllers, DTOs, mapping · `Homebase.Api.Host` composes and serves the frontend. Rules never go in controllers, drivers or SQL.
- **One writer at a time:** every state change runs inside `HomeRuntime.ExclusiveAsync`. Never call it from inside itself (deadlock). Network I/O (discovery, pairing, polling reads) runs outside it.
- **Devices:** `DeviceCommands.Validate` before any driver call; `DeviceReports.Validate` before applying a phone report. Drivers throw `DeviceOfflineException` for an unreachable device and nothing else.
- **Time:** inject `IHouseholdClock`; local time only for `HH:mm` and "today"; store UTC (`Sql.Timestamp`) and serialize UTC with `Z`. Never `DateTime.Now`. Darkness comes from `HomeRuntime.SunNow()`.
- **Wire/DB strings** only through `ApiCodes` / `DbCodes` — never `Enum.ToString()`.
- **Routes (parsed by `npm run contract:check`):** `[Route("api/xxx")]` on its own line above the class; each action has `[HttpGet("template")]` (etc.) on its own line with a relative template. Controllers take services by constructor; register them in `HomebaseApi.AddHomebaseApi`.
- **Errors:** `NotFoundException` → 404; `DomainException(ErrorCodes.X)` → 422 (`unauthorized` → 401); bad JSON → 422 `validation_failed`. New codes go in `ErrorCodes` and the contract; never rename one.
- **Report token:** actions the phone calls carry `[RequireReportToken]`. Never log the token.
- **Wording:** trace and summary text lives only in `Describe` and `AutomationEngine`; tests and the frontend mock copy it — change them together.
- **Async:** `.ConfigureAwait(false)` in library code; never `.Result` / `.Wait()`.
- **Tests (xUnit, all run on any OS):** Domain pure · Devices with fakes (`FakeUdpTransport`, `FakeHttpHandler`), no real network · Application flows on `InMemoryHome` + `TestHome` + `FakeTvDriver` · Data on a temp SQLite file · Api through `ApiServer` (TestServer). Use `FixedClock`; never real time.

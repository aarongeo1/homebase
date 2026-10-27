# 0006. Homebase is a personal "lights follow you" app on the Mac: .NET 10, ASP.NET Core, SQLite

- Status: accepted
- Date: 2026-10-27

## Context
Homebase began as a four-area smart-home hub (Devices, Automations, Energy, Security) on .NET Framework 4.7.2 and
SQL Server. The person it is for has Govee lights in a bedroom and a living room, an Android TV, an Android phone and
an always-on Mac, and two problems: switching lights on when walking into a room, and leaving them on when going out.
They have no locks, alarm or energy meter, and do not want to buy sensors. A Windows-only host and a SQL Server
(amd64-only) container cannot run on that Mac at all.

## Decision
- **Three areas:** Devices (lights, TV, phone), Automations, Presence. Energy and Security are removed, with
  safety rules S1–S5 (nothing left to unlock or disarm) and integer money (ADR-0002).
- **Presence without new hardware:** the phone's automation app *reports* home / charging / asleep
  (`POST /api/devices/{id}/report`, bearer token); the TV's power comes from the Android TV Remote protocol;
  "dark" comes from sunrise/sunset at the configured location. A rule may wait (`forMinutes`) before it fires.
- **Runtime:** every project targets `net10.0`; the host is ASP.NET Core and also serves the built frontend on
  port 5080; data is one SQLite file in `~/Library/Application Support/Homebase`, migrated at startup.
  `npm run mac:install` makes it a launchd agent.
- **Drivers:** Govee LAN (UDP), Govee cloud (Developer API, polled every 5 minutes for its daily limit), Android TV
  (pair once with an on-screen code; read-only). Lights arrive by discovery, never by seed data.
- **The SQL Server migrations were replaced, not migrated:** they never shipped anywhere, so 0001/0002 now
  hold the SQLite schema and seed.

## Consequences
- Good: runs where the person lives; every backend test runs on macOS and Linux (CI has both), so agents can verify
  everything; one process to start; no database server.
- Bad: drivers can only be tested against fakes here — Govee and TV support is UNVERIFIED until tried on the real
  devices. Countdowns live in memory and reset on restart. The report token is the only authentication; other
  endpoints are open to the home network (and Tailscale).
- Enforced by: `backend/Directory.Build.props` (net10.0), CI `macos` job, `.github/instructions/domains/presence.instructions.md`
  (token rules), `Seed_MatchesTheTestHousehold` (seed ↔ test household), `npm run contract:check`.

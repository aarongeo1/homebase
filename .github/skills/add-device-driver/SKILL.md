---
name: add-device-driver
description: Add support for a new kind of hardware (e.g. Philips Hue, a smart plug, another TV brand) by implementing IDeviceDriver in backend/src/Homebase.Devices, with discovery and pairing if it needs them. Use when asked to "support <brand/protocol> devices".
---
# Add a device driver

1. **Research first, cite sources.** Find the device's official docs, preferring a *local* API (no cloud dependency, no rate limit). Record the doc URL in the driver's comment. No official docs? Stop and say so — don't guess endpoints or message formats.
2. Implement `IDeviceDriver` in `backend/src/Homebase.Devices/<Vendor>/`, modelled on `Govee/GoveeLan.cs` (UDP, local) or `Govee/GoveeCloud.cs` (HTTP, cloud): `Key`, `PollInterval`, `ApplyAsync`, `ReadAsync`.
   - Short timeouts (≤ 3 s local, ≤ 8 s cloud). Throw `DeviceOfflineException` with a plain reason when the device doesn't answer; never let other exceptions escape.
   - Pick `PollInterval` from the vendor's rate limit, not from how fresh you'd like the state.
   - Needs a one-time code or button press? Implement `IPairableDriver` (see `AndroidTv/AndroidTvDriver.cs`); keep secrets in `ISettingsRepository`, never in the device row or logs.
   - Can it find devices? Add an `IDeviceDiscovery` that fills `HardwareId`, explains "found nothing" in `Notes` and never throws for it.
3. Wire it up — one PR:
   - register it (and its discovery) in `backend/src/Homebase.Api.Host/Program.cs`, and a stand-in in tests (`FakeLightDriver` or a fake like `FakeTvDriver` in `backend/tests/Homebase.Testing`);
   - a constant in `DriverKeys`;
   - a new migration that widens the `Devices.Driver` CHECK (SQLite needs a table rebuild — see the database instructions);
   - the `DeviceDriver` enum in `contracts/openapi.yaml` (follow `change-api-contract`) and the driver label in the frontend.
4. Tests in `backend/tests/Homebase.Devices.Tests`: exact message bytes, reply parsing, timeouts and errors against fakes (`FakeUdpTransport`, `FakeHttpHandler`). No real network in tests.
5. Mark the driver **UNVERIFIED AGAINST REAL HARDWARE** in its comment until someone tries it on a device, and say so in the PR.

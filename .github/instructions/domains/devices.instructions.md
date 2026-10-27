---
applyTo: "frontend/src/domains/devices/**,backend/src/Homebase.Devices/**"
description: Devices area — lights, the TV and the phone; drivers, discovery and pairing.
---
# Devices area

**Serves:** the person living here, usually on their phone, usually mid-step · **Route:** `/devices`

## What matters to them
A tap shows the state the server confirmed — never an optimistic guess. A light that is offline says why.

## Rules
- Capabilities are fixed per type (`docs/GLOSSARY.md` → *Device capabilities*): lights take `on`, `brightness` (1–100, turns the light on) and `colorTempK` (2000–9000). The TV and phone are read-only (`read_only_device`).
- New lights come only from **discovery** (`POST /api/devices/discover`): added without a room and never duplicated (matched on `HardwareId`). A light found both locally and in the cloud uses `govee-lan` with the cloud's name. People rename and move lights with `PUT /api/devices/{id}`.
- Drivers implement `IDeviceDriver` (pairable ones `IPairableDriver`) in `backend/src/Homebase.Devices`; use the `add-device-driver` skill. Each declares a `PollInterval`. Govee's cloud is rate-limited per day, so cloud lights poll every 5 minutes — don't shorten it.
- Hardware status: Govee LAN **scan and status reads are verified** on H6008 bulbs; its commands, the Govee cloud driver and the Android TV driver are **UNVERIFIED**. Say which in any PR that changes them, and prove behaviour with fakes.
- The TV is watched, never commanded: its protocol only has a power *toggle*, which could switch it on by mistake.
- The simulated driver must keep working with zero hardware.

## When you change this area
- Test message building and reply parsing with canned replies (no real network), plus the offline path.
- macOS asks for local-network permission; if a driver passes its tests but fails on the Mac, check that first.

# Glossary

What Homebase words mean. Agents: use these definitions — don't infer meaning from names.
When a term changes, change it here first.

## Home and devices

| Term | Meaning |
|---|---|
| **Household time** | Local time of the home, from `HOMEBASE_TIMEZONE` (an IANA id such as `America/Edmonton`; default: the Mac's own zone). All `HH:mm` values are household time; all timestamps in the API are UTC with `Z`. |
| **Room** | A place lights belong to: `living-room`, `bedroom`. A newly found light has **no room** (`roomId: null`) until someone puts it in one. |
| **Device** | Anything Homebase controls or watches: a `light`, the `tv` or the `phone`. Has a `type`, a `driver` and a flat `state`. |
| **Driver** | How Homebase talks to a device: `govee-lan` (Govee's local UDP API), `govee-cloud` (Govee's Developer API with an API key), `android-tv` (Android TV Remote protocol), `push` (the device reports to us — the phone), `simulated` (no hardware; tests and trying things out). |
| **Hardware id** | The device's own id (Govee's device id). Discovery uses it so a light is never added twice. |
| **Discovery** | `POST /api/devices/discover`: asks the local network (lights with *LAN Control* on) and, with an API key, the Govee cloud. Adds new lights without a room and follows a light's new IP. |
| **Pairing** | One-time trust between Homebase and the TV: the TV shows a 6-character hex **code**, the person types it in. Until then the TV `needsPairing` and is offline. |
| **Offline** | The driver could not reach the device. Offline lights are shown, disabled, never hidden. |
| **Command** | A request to change a light (`POST /api/devices/{id}/commands`). The TV and phone are **read-only**. |
| **Report** | What the phone says about itself (`POST /api/devices/{id}/report`), e.g. `{ "home": false }`. Only `push` devices report, and only with the **report token**. |
| **Report token** | The shared secret the phone sends as `Authorization: Bearer <token>`. Created once in the data folder; shown only on the Presence page opened on the Mac itself. |

### Device capabilities

| Type | State fields | Controllable? |
|---|---|---|
| `light` | `on`, `brightness` (1–100), `colorTempK` (2000–9000; null while showing a colour) | yes — brightness also turns it on |
| `tv` | `on` (screen on) | no (`read_only_device`) |
| `phone` | `home`, `charging`, `asleep` | no — it reports instead |

## Presence

| Term | Meaning |
|---|---|
| **Home / away** | The phone's `home` field. Null until the phone has reported once. |
| **Dark** | The sun is below the horizon (−0.833°) at the home's location (`HOMEBASE_LATITUDE` / `HOMEBASE_LONGITUDE`). Without a location, dark means **18:00–07:00**. |
| **Sunrise / sunset** | Today's household-local times from the NOAA equations (about a minute's accuracy); null without a location or on a day the sun does not rise or set. |

## Automations

| Term | Meaning |
|---|---|
| **Scene** | A named set of actions applied together ("Bedtime"). Each action succeeds or fails independently. |
| **Automation** | *When* a trigger fires, *only if* all conditions pass, *then* run actions. |
| **Trigger** | `device`: fires when a field (`on`, `home`, `charging`, `asleep`) **changes into** `equals` — edge-triggered, and unknown → value counts. `time`: fires at household `HH:mm` on the listed days (none = every day), at most once per minute. |
| **Countdown** (`forMinutes`) | A device trigger that must keep matching for that long before it fires. `waitingUntil` shows when. If the field changes back first, the run is **cancelled**. Countdowns are lost on restart. |
| **Condition** | `time-window` (`after` inclusive, `before` exclusive; may wrap midnight, e.g. 22:00→03:00), `device` (field equals), `dark` (`equals: true` = dark). All must pass. |
| **Action** | `device` (one light), `room` (every light in the room) or `scene`, with command fields `on`, `brightness`, `colorTempK`. |
| **Run** | One firing of an automation. **Outcome**: `executed`; `skipped` (a condition failed); `failed` (an action failed — the others still ran); `cancelled` (a countdown ended early). |
| **Trace** | Step-by-step explanation of a run or simulation, with observed values ("Sunrise 07:12, sunset 17:48; now is 23:12"). |
| **Simulation** | A dry run as if the trigger fired now (and held for `forMinutes`). Changes nothing. |
| **Loop guard** | Automation-caused changes chain at most 3 deep; an automation never re-triggers itself within its own chain. |

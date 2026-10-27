---
applyTo: "frontend/src/domains/presence/**,backend/src/Homebase.Domain/Sun/**,backend/src/Homebase.Api/Infrastructure/ReportToken.cs,backend/src/Homebase.Api/Controllers/PresenceController.cs"
description: Presence area — home/away from the phone, darkness, the report token and TV pairing. Guards the one secret the phone holds.
---
# Presence area

**Serves:** the person living here, setting up their phone once and then forgetting about it · **Route:** `/presence`

## What matters to them
"You left" must really mean you left. A spoofed report turns the lights off on someone at home; a lost one leaves them on all day.

## Rules
- The phone's automation app reports with `POST /api/devices/{id}/report` and `Authorization: Bearer <token>`. Only `push` devices accept reports (`not_reportable`); a report carries only what changed, and an empty one is 422.
- The token is created once in the data folder (`report-token.txt`, owner-only). It is returned only to **loopback** requests (`GET /api/presence` on the Mac itself). Never log it, put it in a URL, relax the loopback check or the `[RequireReportToken]` filter, or compare it in non-constant time.
- Darkness is computed on the server (`SunCalculator`, NOAA equations, about a minute's accuracy). The browser never computes it.
- TV pairing: `startPairing` makes the TV show a 6-character hex code; `confirmPairing` sends it back. A mistyped code fails before anything reaches the TV (`pairing_failed`).

## When you change this area
- Any new endpoint the phone calls gets a "missing or wrong token" test.
- Don't claim a phone app has a particular trigger unless you can cite its documentation.

---
applyTo: "frontend/src/domains/automations/**,backend/src/Homebase.Domain/Automations/**,backend/src/Homebase.Application/AutomationScheduler.cs,backend/src/Homebase.Application/HomeRuntime.cs"
description: Automations area — triggers, countdowns, conditions, room actions, the engine and Simulate.
---
# Automations area

**Serves:** the person living here, who never wants to touch a light switch · **Route:** `/automations`

## What matters to them
Predictability. "Why did the bedroom turn off?" and "why *didn't* it?" must both be answerable from the run history.

## Engine rules (server-side, `Homebase.Domain`)
- Device triggers are **edge-triggered**: they fire when a field *changes into* `equals` (unknown → value counts), not on every report.
- `forMinutes` starts a countdown (`waitingUntil`). The scheduler fires it if the field still matches, otherwise records `cancelled`; a report that ends it early also records `cancelled`, saying how long it held. Countdowns live in memory, so a restart drops them (accepted).
- Time windows may **wrap midnight**; `after` inclusive, `before` exclusive. Test both sides of midnight.
- `dark` uses sunrise/sunset from `HOMEBASE_LATITUDE`/`HOMEBASE_LONGITUDE`, otherwise 18:00–07:00; the trace says which.
- Room actions apply to every light in the room. One offline light fails the action (a `failed` run) but the others still change.
- Outcomes: `executed` · `skipped` (a condition failed) · `failed` (an action failed) · `cancelled` (a countdown ended early).
- Loop guard: chains stop at depth 3; an automation never re-triggers itself within its own chain.
- Every decision adds a `TraceStep` with the observed values. New rule → new trace.

## Frontend
Render summaries, traces and `waitingUntil` exactly as the server sends them. Simulate is a dry run — it never changes state.

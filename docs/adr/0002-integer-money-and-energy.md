# 0002. Money is integer cents, energy is integer watt-hours, math is server-side

- Status: superseded by 0006 (the Energy area was removed; nothing in Homebase handles money any more)
- Date: 2026-10-26

## Context
Floating-point money (`0.1 + 0.2`) produces visible cent errors on cost reports. Models trained on tutorial code
reach for `toFixed`, `* 100` and `decimal`-in-JSON by default, and duplicating rules in the browser guarantees drift.

## Decision
- API: `Money = { amountCents: int64, currency }`; energy fields are integer `Wh`.
- Database: `BIGINT` cents + `CHAR(3)`; integer Wh. No `FLOAT`/`DECIMAL` for either.
- Cost = round-half-away-from-zero(Σ Wh × centsPerKwh ÷ 1000), integer math, rounded once per device and once for the total.
- All money, energy, automation and safety logic lives in `backend/src/Homebase.Domain`. The frontend only formats.

## Consequences
- Good: one tested implementation; totals reproducible from the database.
- Bad: per-device costs may not sum exactly to the total (documented in the glossary — don't "fix" it).
- Enforced by (until 0006): domain unit tests, CHECK constraints, the Energy area instruction file, reviewer agent checklist.

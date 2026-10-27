# 0001. Areas with enforced boundaries

- Status: accepted (the four areas below became Devices, Automations and Presence in 0006; the boundary rules are unchanged)
- Date: 2026-10-26

## Context
Homebase serves different people in the same house: everyone taps lights (Devices), one person sets up rules
(Automations), someone pays the bill (Energy), and the homeowners care about doors and the alarm (Security).
The areas have different risk levels and different rules. Without boundaries, every change drags every area's rules
into an agent's context (cost, confusion), and agents "helpfully" import across areas, creating coupling nobody chose.

## Decision
- Frontend areas live in `frontend/src/domains/<area>` and expose a single public API (`index.ts` → `<area>Module`).
  The shell composes them through the `DomainModule` contract.
- Cross-area imports only through `@/domains/<area>`; `shared/` never imports areas or the shell.
- Each area has its own agent context (`.github/instructions/domains/<area>.instructions.md`) that loads only when
  files in that area are touched. High-risk paths require the maintainer's review (CODEOWNERS).

## Consequences
- Good: an agent working on energy loads energy's rules, not security's; a change in one area can't silently break another.
- Bad: some duplication between areas is accepted instead of premature shared abstractions.
- Enforced by: `frontend/eslint.config.js` (`no-restricted-imports`), `.github/CODEOWNERS`, `frontend/src/app/routes.test.tsx`.

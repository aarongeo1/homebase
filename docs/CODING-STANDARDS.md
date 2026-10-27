# Engineering standards

One set of rules for everyone who writes code here, whether a person or GitHub Copilot. Anything needed to make a
change must be written down, and anything that must never happen must be **enforced**, not just written down.

Each practice below names the file or check that implements it.

## 1. Codified project instructions

| What | Where | Loaded |
|---|---|---|
| Architecture, constraints, universal rules | [`.github/copilot-instructions.md`](../.github/copilot-instructions.md) | every request |
| Frontend, backend, database, contract rules | [`.github/instructions/*.instructions.md`](../.github/instructions/) | when you touch matching files |
| Area knowledge (who it serves, invariants) | [`.github/instructions/domains/`](../.github/instructions/domains/) | when you touch that area |
| Project entry point | [`AGENTS.md`](../AGENTS.md) | every request |

Rules are short, imperative and explain *why*. If a linter already enforces something, it is not repeated here.

## 2. Reusable skills and prompts

| Task | Use |
|---|---|
| Plan a feature | `/plan-feature` → Planner agent |
| Fix a bug test-first | `/fix-bug` → Implementer agent |
| Add tests | `/write-tests` → Test Writer agent (economy model) |
| Understand code | `/explain` → Codebase Guide agent (economy model) |
| Add an area / endpoint / device driver / ADR | skills `scaffold-domain`, `change-api-contract`, `add-device-driver`, `write-adr` |

Skills load only when relevant (their description is all the agent sees until then), so long procedures cost nothing until used.

## 3. Shared guidelines (the non-negotiables)

1. **Follow the existing stack.** React 19 + TS 5.9 + React Router 8; ASP.NET Core on .NET 10; SQLite via Dapper with embedded migrations.
   No new dependency without an ADR.
2. **Ground every claim** in code you read (`path:line`). If it's not in [`docs/CODEMAP.md`](CODEMAP.md), it doesn't exist.
3. **Use the glossary.** Business words mean what [`docs/GLOSSARY.md`](GLOSSARY.md) says.
4. **Rules live on the server.** Presence, darkness and automations are decided in `backend/src/Homebase.Domain`.
5. **Contract first.** The API changes in `contracts/openapi.yaml` before code (skill: `change-api-contract`).
6. **Migrations are append-only.** Never edit a shipped `database/migrations` script.
7. **The report token has no bypass.** Not a flag, not a "dev mode", not a test helper; and no secret in code, logs or tests.
8. **Never weaken a check to get green** — tests, lint rules, CHECK constraints, hooks.

## 4. Copilot safety loops

Agents edit, run commands and read the output to correct themselves. We make that loop short and honest:

- **One verify command** — `npm run verify -- <paths>` runs only the gates those paths affect and prints a compact result.
  Skipped gates are reported as gaps, never as passes.
- **3-strike rule** — same error three times → stop and report. Loops burn budget and rarely converge.
- **Guardrail hooks** — [`.github/hooks/guardrails.json`](../.github/hooks/guardrails.json) blocks force-pushes, pushes to main,
  `--no-verify`, editing generated files or shipped migrations, reading `.env`, and destructive SQL. Tested in
  `scripts/hooks/guardrails.test.mjs`.
- **CI is the final gate** — Linux (`verify --strict`) and macOS (backend tests, where Homebase actually runs) on every PR.

> Tool names have moved on: older guides list `read_file`, `edit_file`, `run_in_terminal`. Portable custom-agent
> aliases are now `read`, `edit`, `execute`, `search`, `agent`, `web`, `todo` (see [STAYING-CURRENT](ai/STAYING-CURRENT.md)).

## 5. How we work with Copilot

### Plan first, code second
Don't ask for a whole feature in one prompt. Planner (premium model, read-only) writes a ≤ 40-line plan with evidence,
risks and open questions → **you edit and approve it** → Implementer (standard model) executes step by step.

### Test-driven acceptance
Every acceptance criterion becomes a test. Bugs start with a failing test that reproduces them.
"Implement until the tests pass" — but the tests are written or approved by a human first.

### Keep scope tight
One area per PR where possible; roughly ≤ 10 files. Agents degrade as context bloats, so big refactors are split into
verifiable steps (or done by humans with agent help). Start a new chat per task.

### Review is mandatory
You are the tech lead; the agent is the intern. Read every diff. The Reviewer agent gives an independent second read,
CODEOWNERS makes your review mandatory, and **report-token and hardware-driver changes are human-led** with agent assistance — the agent cannot try a real light or TV.

## 6. Definition of done

- [ ] Behaviour matches the acceptance criteria, including the unhappy path
- [ ] Tests fail without the change and pass with it
- [ ] `npm run verify -- <paths>` passes; any SKIPPED gate is named in the PR
- [ ] Contract, backend and frontend move together; codemap regenerated
- [ ] PR template filled in, including Copilot workflow and confidence

# Homebase — Copilot instructions

Homebase makes one person's lights follow them: Govee lights in two rooms, an Android TV and an Android phone,
run from a Mac. React 19 + TypeScript frontend, ASP.NET Core on **.NET 10**, **SQLite**. Three areas:

| Area | Frontend | Backend | Does | Risk |
|---|---|---|---|---|
| Devices | `frontend/src/domains/devices` | `backend/src/Homebase.Devices` | lights, TV, phone; drivers, discovery, pairing | medium (real hardware) |
| Automations | `frontend/src/domains/automations` | `backend/src/Homebase.Domain/Automations` | rules, countdowns, Simulate | medium |
| Presence | `frontend/src/domains/presence` | `backend/src/Homebase.Domain/Sun` | home/away, darkness, phone report token | medium (secret) |

## Look things up cheaply, in this order
1. `docs/CODEMAP.md` — every endpoint, export, public type and table. Generated. **Not listed = does not exist.**
2. `contracts/openapi.yaml` — the only API. Frontend types are generated from it.
3. `docs/GLOSSARY.md` — what domain words mean (report, countdown, dark, driver). Don't infer meaning from names.
4. `docs/adr/` — why things are the way they are. Area rules load automatically from `.github/instructions/`.

## Rules
- **Ground every claim.** Cite `path:line` for statements about existing code. Mark anything you did not read as *unverified*.
- **Never invent** APIs, props, endpoints, columns, config keys, packages, versions or device protocols. Missing? Say so and propose adding it.
- **Versions are pinned and newer than your training data**: React Router 8 (no `react-router-dom`), TypeScript 5.9, .NET 10. Check `package.json` / `Directory.Packages.props` before using a library API.
- **Ambiguous request → ask** (max 3 questions) or list your assumptions before writing code.
- **Small diffs.** Change only what the task needs. No drive-by refactors or formatting churn.
- **Business rules live on the server.** Presence, darkness and automation decisions happen in `backend/src/Homebase.Domain`; the frontend renders what the API returns.
- **No real hardware in your sandbox.** Prove driver behaviour with fakes and say what still needs a real device. Drivers never tried on hardware stay marked UNVERIFIED.
- **Secrets never in code, tests, logs or docs** (Govee API key, report token, TV certificate). Never weaken the report-token check, a test, lint rule, CHECK constraint or guardrail to make something pass.

## Definition of done
Run `npm run verify -- <paths you changed>` and paste its last line. "Should work" is not done.
After **3 failed attempts** at the same error, stop and report what you tried — don't loop.
If verify says SKIPPED (e.g. no .NET SDK), list exactly what was not verified.

## Reply format
**Changed** (files) · **Verified** (command + result line) · **Confidence** high/medium/low + why · **Not verified / open questions**.

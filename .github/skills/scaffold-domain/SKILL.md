---
name: scaffold-domain
description: Create a new area (domain) in Homebase — frontend module, backend folder, routing and agent context. Use when asked to add a new area/section such as "Plants" or "Sleep". Not for adding a page to an existing area.
---
# Scaffold a new area

Confirm first (ask if missing): area id (kebab-case), title, what it does for the person living here, URL segment, risk level.

## Frontend
1. Create `frontend/src/domains/<id>/` with `index.ts`, `module.tsx`, `pages/`, `components/`.
   Copy the shape of `frontend/src/domains/presence/module.tsx` (the smallest complete example).
2. `index.ts` exports only `<id>Module` — the area's public API.
3. Register the module in `frontend/src/app/domain-registry.ts`. The route, nav and home card appear automatically.

## Backend
4. Put rules in `backend/src/Homebase.Domain/<Area>/` with tests in `backend/tests/Homebase.Domain.Tests`.
5. Add endpoints via the `change-api-contract` skill (contract first).

## Agent context and review
6. Copy [templates/domain.instructions.md](templates/domain.instructions.md) to `.github/instructions/domains/<id>.instructions.md` and fill every section. Keep it under ~600 tokens.
7. Add `CODEOWNERS` lines for the frontend folder, backend folder and the instruction file (higher-risk areas always need the maintainer's review).
8. Add the area's terms to `docs/GLOSSARY.md`.

## Finish
9. `npm run codemap && npm run verify` — paste the last line.

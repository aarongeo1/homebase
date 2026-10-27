---
applyTo: "contracts/**,frontend/src/shared/api/**,backend/src/Homebase.Api/Contracts/**,backend/src/Homebase.Api/Controllers/**"
description: Rules for changing the API contract between frontend and backend.
---
# API contract

`contracts/openapi.yaml` is the source of truth. Three things must move together, in one PR:

1. `contracts/openapi.yaml` — the change, with descriptions for any rule a client must know.
2. Backend DTOs/controllers — names, nullability and string values exactly as in the contract.
3. Frontend — `npm run gen:api` (never edit `generated/` by hand), then the `api` facade in `frontend/src/shared/api/client.ts`, then `mock/mock-fetch.ts`.

- Follow the `change-api-contract` skill for the full checklist.
- Prefer additive, backward-compatible changes. Removing/renaming a field or value is breaking: say so explicitly.
- Never make a field optional or nullable just to silence a type error — find out why it's missing.
- `npm run verify -- contracts` checks generated types are fresh, routes match (`contract:check`) and the mock implements every operation.

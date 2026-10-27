---
name: change-api-contract
description: Safely add or change an API endpoint, field or enum value across contracts/openapi.yaml, the ASP.NET Core backend and the React frontend. Use whenever a change crosses the frontend/backend boundary.
---
# Change the API contract

Order matters: contract → backend → frontend. One PR.

1. **Contract** — edit `contracts/openapi.yaml`. Add `description` for any rule a client must know (units, ranges, who may call it). Prefer additive changes; call out anything breaking.
2. **Backend**
   - DTO in `backend/src/Homebase.Api/Contracts/` matching names, nullability and string values exactly.
   - Mapping code for kebab-case values (no `Enum.ToString()`).
   - Controller action with `[HttpGet("template")]` (etc.) on its own line under a class-level `[Route("api/...")]`; a new service a controller needs is registered in `backend/src/Homebase.Api/HomebaseApi.cs`.
   - Endpoints the phone calls get `[RequireReportToken]` and a missing/wrong-token test.
   - Rule changes go in `Homebase.Domain` with xUnit tests; data in `Homebase.Data` (+ a new migration if the schema changes); an API test in `backend/tests/Homebase.Api.Tests`.
3. **Frontend**
   - `npm run gen:api` (never edit `generated/`).
   - Add a type alias in `frontend/src/shared/api/types.ts` and a method on `api` in `client.ts`.
   - Implement the operation in `frontend/src/shared/api/mock/mock-fetch.ts` (+ fixtures). The contract test fails until you do.
4. **Check** — `npm run verify -- contracts backend frontend/src/shared/api`. Expect `contract:types`, `contract:routes` and the mock coverage test to pass.
5. `npm run codemap`, then describe the change (and any breaking aspect) in the PR.

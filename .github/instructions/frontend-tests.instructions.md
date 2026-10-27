---
applyTo: "frontend/src/**/*.test.ts,frontend/src/**/*.test.tsx"
description: How to write frontend tests (Vitest 5 + Testing Library).
---
# Frontend tests

- Import test APIs explicitly: `import { describe, expect, it } from 'vitest'`.
- Page/component tests render real routes against the mock API with `renderAt('/path')` from `@/test/render`; it returns `user` (user-event).
- Mock data lives in `frontend/src/shared/api/mock/fixtures.ts` and is reset after every test automatically. Don't mock `api` by hand.
- Query by role and accessible name (`getByRole('button', { name: 'Find lights' })`), then label, then text. No test IDs, no CSS selectors, no snapshots.
- Await UI that appears after a request: `await screen.findBy…`.
- Test behaviour a user would notice, including the unhappy path (422 errors, offline lights, the read-only TV and phone, a wrong pairing code).
- **Never delete or loosen an assertion to make a test pass.** If a test looks wrong, say why and ask.
- Bug fix? First add a test that fails for the reported reason, show it failing, then fix.

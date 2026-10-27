# 0003. Exact versions, and TypeScript stays on 5.9

- Status: accepted
- Date: 2026-10-26

## Context
Agents write code for the library version they remember, not the one installed. When this repo was created,
the latest versions were newer than most models' training data: React Router **8** (which removed `react-router-dom`),
TypeScript **7** (the native Go compiler), Vite 8, ESLint 10, Vitest 5. TypeScript 7 also broke the toolchain:
`typescript-eslint` 8.72 supports `typescript <6.1` and `openapi-typescript` 7.13 supports `^5`.

## Decision
- npm: exact versions (`save-exact=true`); NuGet: Central Package Management with exact versions (`backend/Directory.Packages.props`).
- TypeScript is pinned to **5.9.x** until both tools above support a newer major. Dependabot ignores TypeScript ≥ 6.
- `.github/copilot-instructions.md` lists the versions most likely to be misremembered.

## Consequences
- Good: reproducible builds; "works on my machine" and "works in the agent's memory" stop being different things.
- Bad: upgrades are deliberate work (Dependabot PRs + this ADR updated).
- Enforced by: `.npmrc`, `.github/dependabot.yml`, ESLint ban on `react-router-dom`, `npm run verify`.

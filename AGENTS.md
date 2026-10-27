# Homebase project guidance

Homebase: React + TypeScript (`frontend/`), .NET 10 ASP.NET Core API (`backend/`), SQLite migrations (`database/`),
API contract in `contracts/openapi.yaml`. Runs on a Mac; drives Govee lights, watches an Android TV, listens to an Android phone.

Project rules live in [.github/copilot-instructions.md](.github/copilot-instructions.md) (read it first) and,
per area, in [.github/instructions/](.github/instructions/). They apply to Copilot throughout the repository.

- Setup: `npm ci` (Node ≥ 22.22) · .NET SDK 10. No database server: the SQLite file is created on first start.
- Run: `npm run dev` → http://localhost:5173 (mock API, no hardware) · `npm start` → http://localhost:5080 (real backend)
- Check: `npm run verify -- <path>` before claiming done
- Map: `docs/CODEMAP.md` · Words: `docs/GLOSSARY.md`

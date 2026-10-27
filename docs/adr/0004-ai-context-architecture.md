# 0004. How agent context is organised

- Status: accepted
- Date: 2026-10-26

## Context
Since June 2026 Copilot bills by tokens × model rate, so every always-on instruction is paid on every request.
Large instruction files also dilute attention and contradict each other; stale ones send agents to files that no longer exist.

## Decision
| Layer | File | Loaded |
|---|---|---|
| Repo-wide rules | `.github/copilot-instructions.md` (canonical) | always |
| Cross-tool bridge | `AGENTS.md` (short, points to the above) | always |
| Tech conventions | `.github/instructions/*.instructions.md` | when `applyTo` matches |
| Area knowledge | `.github/instructions/domains/*.instructions.md` | when `applyTo` matches |
| Procedures | `.github/skills/*/SKILL.md` | on demand (description only until used) |
| Workflows | `.github/agents/*.agent.md`, `.github/prompts/*.prompt.md` | when chosen |
| Facts | `docs/CODEMAP.md` (generated), `docs/GLOSSARY.md`, `docs/adr/` | when read |
| Guardrails | `.github/hooks/`, CI, lint, tests | always enforced |

`copilot-instructions.md` is canonical because it is the one file every Copilot surface reads (including Chat on github.com).
Path-scoped `.instructions.md` beats nested `AGENTS.md` here because it works in VS Code, the CLI, the cloud agent and code review alike.

## Consequences
- Good: always-on context stays under ~1.5k tokens; each area's rules sit next to the reason they exist; drift is caught by lint.
- Bad: two root files (copilot-instructions + AGENTS.md) must not contradict — AGENTS.md only points.
- Revisit when: Copilot Chat on github.com reads AGENTS.md (then AGENTS.md could become canonical), or path-scoped instructions gain a cross-tool standard.
- Enforced by: `scripts/lint-ai-config.mjs` (budgets, globs, links, fields, models), CODEOWNERS.

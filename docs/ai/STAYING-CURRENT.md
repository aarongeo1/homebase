# Staying current

Copilot's customization surface changes monthly. This repo was checked against the official docs on **2026-10-26**.
Here is what changed recently, where to watch, what signals matter, and the automation that keeps the repo honest.

## What changed recently

| Change | Old | New | Handled here by |
|---|---|---|---|
| Chat modes became **custom agents** | `.github/chatmodes/*.chatmode.md` | `.github/agents/*.agent.md` | lint rejects `.github/chatmodes/` |
| Agent visibility flags | `infer` | `user-invocable`, `disable-model-invocation` (`infer` retired) | lint `DEPRECATIONS` |
| Prompt file frontmatter | `mode:` | `agent:` | lint `DEPRECATIONS` |
| "Coding agent" renamed | `excludeAgent: coding-agent` | `cloud-agent` | lint `RENAMED_VALUES` |
| New surfaces | — | **Agent Skills** (`.github/skills`), **hooks** (`.github/hooks/*.json`) | used in this repo |
| Portable tool names | `read_file`, `edit_file`, `run_in_terminal` | `read`, `edit`, `execute`, `search`, `agent`, `web`, `todo` | lint warns on unknown tools |
| Billing | premium requests × multipliers | **usage-based** GitHub AI Credits (tokens × model rate), since 2026-06-01 | budgets in PLAYBOOK, token budgets in lint |
| Stack drift | `react-router-dom`, TS 5 assumed latest | React Router 8 removed `react-router-dom`; TS 7 is native and breaks lint tooling | ADR-0003, ESLint ban |

## Where to watch

| Source | Why | Cadence |
|---|---|---|
| [GitHub Changelog — Copilot](https://github.blog/changelog/label/copilot/) | New features, renames, model launches/retirements, billing | weekly (RSS) |
| [VS Code release notes](https://code.visualstudio.com/updates) | Agent mode, custom agents, prompt/instruction settings | monthly |
| [Supported models](https://docs.github.com/en/copilot/reference/ai-models/supported-models) | Model names for `.github/ai-models.json` | monthly |
| [Custom agents configuration](https://docs.github.com/en/copilot/reference/custom-agents-configuration) | Frontmatter fields and tool aliases | quarterly |
| [Hooks reference](https://docs.github.com/en/copilot/reference/hooks-reference) | Events, payloads, decision format | quarterly |
| [Repository custom instructions](https://docs.github.com/en/copilot/how-tos/configure-custom-instructions/add-repository-instructions) | Which surface reads which file | quarterly |
| [VS Code custom instructions](https://code.visualstudio.com/docs/copilot/customization/custom-instructions) | `applyTo`, AGENTS.md and settings | quarterly |
| [Usage-based billing](https://docs.github.com/en/copilot/concepts/billing/usage-based-billing-for-individuals) | Monthly credits and budget | when billing changes |
| [github/awesome-copilot](https://github.com/github/awesome-copilot) | Community agents, instructions, skills | monthly skim |
| [AGENTS.md](https://agents.md) · [Agent Skills](https://agentskills.io) · [MCP spec](https://modelcontextprotocol.io/specification) | Open standards (Agentic AI Foundation) Copilot follows | quarterly |

## Signals to act on

1. **A model is added, re-priced or retired** → update `.github/ai-models.json` (and `lastVerified`); `npm run lint:ai` shows every agent to change.
2. **A field or file is renamed/retired** → add it to `DEPRECATIONS`/`RENAMED_VALUES` in `scripts/lint-ai-config.mjs` *first*, then fix what it flags.
3. **A new customization surface appears** → write an ADR before adopting; check which surfaces support it (VS Code, CLI, cloud agent, code review often differ).
4. **Surface parity changes** (e.g. Chat on github.com starts reading AGENTS.md, handoffs reach the cloud agent) → revisit ADR-0004.
5. **A major version of something in our stack ships** → agents will write the old API for months. Pin, add a line to the version notes, add a lint ban if possible.
6. **Security advisories** about prompt injection, MCP servers or hook bypasses → review tools per agent and the MCP allowlist.
7. **Mistakes our agents make** → the most important signal. Fix at the cheapest durable layer (lint > test > type > hook > instruction).

## Automation in this repo

| What | When | Fails when |
|---|---|---|
| `npm run lint:ai` | every PR (via verify) | broken links/paths, dead `applyTo` globs, unknown/retired fields, unapproved models, budgets exceeded |
| `ai-config-health.yml` | weekly, Monday | the above in `--strict`, plus model list older than 90 days; posts npm/NuGet drift report |
| Dependabot | weekly | — (opens PRs; TypeScript ≥ 6 ignored per ADR-0003) |
| `npm run codemap -- --check` | every PR | codemap stale |

## Quarterly review (30 minutes)

- [ ] Skim the changelog since the last review; update `DEPRECATIONS` and the table above.
- [ ] Re-verify `.github/ai-models.json` against the supported-models page; bump `lastVerified`.
- [ ] Read five agent PRs that needed rework; turn each lesson into a lint rule, test or instruction line.
- [ ] Delete instruction lines nobody needed (if removing a line changes nothing, it was noise).
- [ ] Check credits per merged PR by area and agent; adjust model tiers.
- [ ] Confirm hooks still match the current hook payload format.

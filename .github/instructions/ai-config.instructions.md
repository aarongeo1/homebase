---
applyTo: ".github/**/*.md,**/AGENTS.md,.github/ai-models.json,.github/hooks/**"
description: How to write and change agent context (instructions, agents, prompts, skills, hooks).
---
# Changing the AI context layer

Agent context is code: reviewed, owned (CODEOWNERS), linted (`npm run lint:ai`).

- **Prefer enforcement over instructions.** If a rule can be a type, test, lint rule, CHECK constraint or hook, do that instead; instructions are the weakest control.
- Always-on files (`.github/copilot-instructions.md`, `AGENTS.md`) have a ~1500-token budget. Put area rules in `.github/instructions/*.instructions.md` with a precise `applyTo`, and long procedures in skills.
- One short, imperative rule per bullet, with the *why* when it isn't obvious. Show ✅/❌ code for patterns.
- Don't restate what a linter/formatter already enforces. Link to docs instead of copying them.
- No contradictions between files — Copilot's choice between conflicting instructions is non-deterministic.
- Models must come from `.github/ai-models.json`. Agents declare `tools` explicitly (least privilege).
- After any change: `npm run lint:ai`. It checks links, globs, fields, models, budgets and deprecations.

---
name: write-adr
description: Record an architecture decision (new dependency, pattern, version pin, cross-area rule) as an ADR in docs/adr. Use when a change introduces or reverses a technical decision others must follow.
---
# Write an ADR

1. Next number: look at `docs/adr/` and use the next `NNNN`.
2. Copy [template.md](template.md) to `docs/adr/NNNN-short-title.md` and fill it in. Keep it under one page.
3. Write **Consequences** for agents too: which instruction file, lint rule or test now encodes the decision.
4. Link the ADR from the instruction file it affects, then run `npm run lint:ai`.

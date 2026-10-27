## What and why
<!-- One or two sentences. Link the issue. -->

## Areas touched
- [ ] Devices  - [ ] Automations  - [ ] Presence  - [ ] Contract  - [ ] Database  - [ ] AI config

## Verification
<!-- Paste the LAST LINE of `npm run verify -- <paths>`. List anything SKIPPED and why. -->
```
VERIFY ...
```

## Copilot workflow
- Copilot mode / model used: <!-- e.g. Planner → Implementer → Reviewer, or "none" -->
- What a human checked line by line:
- Confidence (high / medium / low) and why:
- Not verified / open questions:

## Checklist
- [ ] Tests fail without this change and pass with it
- [ ] Report-token check and loopback-only token display intact; no secret in code, logs or tests
- [ ] Driver changes: says whether it was tried on a real device (else it stays UNVERIFIED)
- [ ] No test, lint rule, constraint or guardrail loosened
- [ ] Contract changes follow the `change-api-contract` skill (contract → backend → frontend)
- [ ] New migration instead of editing a shipped one
- [ ] `docs/CODEMAP.md` regenerated if files/exports changed

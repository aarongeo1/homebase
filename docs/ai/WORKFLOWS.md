# Working with Copilot day to day

## Cheat sheet

| I want to… | Use | Model tier | Notes |
|---|---|---|---|
| Understand how something works | `/explain` → Codebase Guide | economy | Cites `path:line`; says "not found" instead of guessing |
| Build a feature | `/plan-feature` → Planner → **you edit the plan** → Implementer → Reviewer | premium → standard → standard | Handoff buttons between steps |
| Fix a bug | `/fix-bug` → Implementer | standard | Failing test first |
| Add tests | `/write-tests` → Test Writer | economy | Reports bugs, doesn't fix them |
| Review before a PR | Reviewer agent | standard | Runs verify itself |
| Hand off async work | Issue → **Copilot task** form → assign to Copilot | cloud agent | Fill every field; small and verifiable |
| Add an area / endpoint / driver | ask in agent mode; skills load automatically | standard | `scaffold-domain`, `change-api-contract`, `add-device-driver` |

## Walkthrough: a feature, end to end

> "Let me pause all automations for 2 hours."

1. **Plan** — `/plan-feature automations: pause all automations for 2 hours`. The Planner (read-only) reads the codemap,
   the automations area file and the glossary, then returns Goal / Evidence / Steps / Risks / Open questions / Confidence.
   It should flag what happens to a countdown that is waiting when the pause starts.
2. **Edit the plan.** This is the highest-leverage minute of the whole task. Cut scope, answer the open questions.
3. **Implement** — click *Implement this plan*. The Implementer goes contract → backend → frontend, running
   `npm run verify -- contracts backend frontend/src/domains/automations` after each step.
4. **Review** — click *Review my changes*. The Reviewer re-derives the change from `git diff` and runs verify itself.
5. **PR** — fill the template: verify's last line, Copilot workflow, confidence, and what wasn't verified.
   Copilot code review adds a pass using the same instruction files; then you read the diff and merge.

## Walkthrough: delegate to the cloud agent

1. Open an issue with the **Copilot task** form. Acceptance criteria as testable bullets; pointers to files.
2. Assign it to Copilot. `copilot-setup-steps.yml` has already prepared Node, .NET and dependencies.
3. The agent works on a branch, runs verify (hooks block anything dangerous) and opens a draft PR.
4. Review it like any PR. Comment to request changes — each round costs credits, so be specific.

## Cost hygiene for developers

- **Pick the agent, not the biggest model.** The agents already carry the right tier.
- **New chat per task.** Long chats re-send their whole history on every turn.
- **Point, don't paste.** Reference files (`#file`) or name them; don't paste whole files or logs.
- **Don't say "look around the codebase".** Say which area; the codemap does the looking.
- **Stop at strike three.** If an agent fails the same way three times, re-plan or take over.
- **Scope verify.** `npm run verify -- <path>` is faster and its output is shorter than a full run.

## Reading an agent's answer

Trust it in proportion to its evidence:
- Claims with `path:line` you can click → probably right; spot-check one.
- "Verified: VERIFY PASSED — 9/9 gates" → good. "PASSED WITH GAPS" → read which gates were skipped.
- No citations, no verify line, confidence "high" → treat as a draft.

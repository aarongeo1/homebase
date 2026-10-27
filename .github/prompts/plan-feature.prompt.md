---
description: Plan a new feature in one area before writing code (routes to the Planner agent).
agent: planner
argument-hint: "<area>: <what someone at home should be able to do>"
---
Plan this feature: ${input:feature:automations: let me pause all automations for 2 hours}

Before planning:
- Identify the area and read its rules in [the domain instructions](../instructions/domains/).
- Check [the code map](../../docs/CODEMAP.md) for components, endpoints and types to reuse.
- Look up every business term in [the glossary](../../docs/GLOSSARY.md).

Acceptance criteria must be testable, one bullet per behaviour, including the unhappy path.
If the request is ambiguous, ask up to 3 questions instead of planning.

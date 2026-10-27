---
description: Add tests for existing code without changing behaviour (Test Writer agent, economy model).
agent: test-writer
argument-hint: "File or behaviour to cover"
---
Write tests for: ${input:target:frontend/src/domains/presence/components/TvPairing.tsx}

List the behaviours and edge cases you will cover first, then write the tests, then run verify.
Follow [frontend test rules](../instructions/frontend-tests.instructions.md) or the xUnit conventions in
[backend rules](../instructions/backend-dotnet.instructions.md).

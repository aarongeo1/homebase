---
description: Fix a bug test-first — reproduce with a failing test, fix minimally, verify (Implementer agent).
agent: implementer
argument-hint: "What happened vs what you expected, and where"
---
Fix this bug: ${input:bug:the bedroom stayed on after I plugged my phone in at 23:30}

1. **Reproduce.** Write a test that fails for the reported reason. Run it and show the failure. If you cannot reproduce it, stop and report what you tried.
2. **Root cause.** Explain it in one or two sentences with `path:line`.
3. **Fix** with the smallest change that makes the test pass. Don't refactor.
4. **Verify.** `npm run verify -- <changed paths>`.
5. **Report** root cause, fix, verify's last line, confidence, and anything not verified.

#!/usr/bin/env node
// Copilot preToolUse hook entry point. Reads the event JSON on stdin, prints exactly one
// JSON object on stdout. On our own bug we print `{}` (no opinion) rather than exiting
// non-zero, because a crashing hook would deny every tool call (fail closed).
import path from 'node:path';
import { decide, readPayload } from './guardrails.mjs';

const repoRoot = path.resolve(import.meta.dirname, '..', '..');
let input = '';
process.stdin.setEncoding('utf8');
process.stdin.on('data', (chunk) => (input += chunk));
process.stdin.on('end', () => {
  try {
    const decision = decide(readPayload(input || '{}'), repoRoot);
    process.stdout.write(JSON.stringify(decision ?? {}));
  } catch (error) {
    process.stderr.write(`guardrails hook error (allowing): ${error.message}\n`);
    process.stdout.write('{}');
  }
});

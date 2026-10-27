#!/usr/bin/env node
// One command for every quality gate, with output short enough to paste as evidence.
//
//   npm run verify                      # everything
//   npm run verify -- frontend/src/domains/presence    # only the gates that path affects
//   npm run verify -- --strict          # CI: a skipped gate (e.g. no .NET SDK) is a failure
//
// Agents: run this before saying "done" and paste the final summary line.
// Output is deliberately compact — every line you print costs tokens in an agent's context.
import { spawnSync } from 'node:child_process';
import { existsSync, mkdtempSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { findDotnet } from './dotnet.mjs';

const ROOT = path.resolve(import.meta.dirname, '..');
const args = process.argv.slice(2);
const strict = args.includes('--strict');
const scopes = args.filter((a) => !a.startsWith('--')).map((a) => path.relative(ROOT, path.resolve(a)).split(path.sep).join('/'));
const isWindows = process.platform === 'win32';
const TAIL_LINES = 40;

const touches = (...prefixes) => scopes.length === 0 || scopes.some((s) => prefixes.some((p) => s === p || s.startsWith(`${p}/`) || p.startsWith(`${s}/`) || s === ''));
const frontendScope = scopes.filter((s) => s.startsWith('frontend/')).map((s) => s.slice('frontend/'.length));

function run(cmd, cmdArgs, cwd = ROOT, env = {}) {
  const started = Date.now();
  const result = spawnSync(cmd, cmdArgs, {
    cwd,
    encoding: 'utf8',
    shell: isWindows,
    env: { ...process.env, FORCE_COLOR: '0', NO_COLOR: '1', DOTNET_NOLOGO: '1', DOTNET_CLI_TELEMETRY_OPTOUT: '1', ...env },
    maxBuffer: 64 * 1024 * 1024,
  });
  return { ok: result.status === 0, output: `${result.stdout ?? ''}${result.stderr ?? ''}`, ms: Date.now() - started };
}

const npx = (bin, binArgs, cwd) => run(path.join(ROOT, 'node_modules', '.bin', bin), binArgs, cwd);

/** Pull one short "what happened" line out of a tool's output. */
function summarize(name, output) {
  const lines = output.split('\n').map((l) => l.trim());
  const pick = (re) => lines.filter((l) => re.test(l)).pop();
  if (name === 'frontend:test') return pick(/^Tests\s+\d+/) ?? '';
  if (name.startsWith('backend:test')) return lines.filter((l) => /^(Passed!|Failed!|Skipped!)|Total tests|^Passed:|^Failed:/.test(l)).slice(-2).join(' · ');
  if (name === 'backend:build') return pick(/Warning\(s\)|error\(s\)|Build succeeded/i) ?? '';
  return pick(/\b(ok|passed|clean|up to date|errors?)\b/i) ?? '';
}

const gates = [];
const gate = (name, applies, fn) => applies && gates.push({ name, fn });

// ── Frontend ────────────────────────────────────────────────────────────────
const FRONTEND = path.join(ROOT, 'frontend');
gate('frontend:typecheck', touches('frontend', 'contracts'), () => npx('tsc', ['--noEmit', '-p', 'tsconfig.json'], FRONTEND));
gate('frontend:lint', touches('frontend'), () => npx('eslint', [...(frontendScope.length ? frontendScope : ['.']), '--max-warnings', '0'], FRONTEND));
gate('frontend:test', touches('frontend', 'contracts'), () => npx('vitest', ['run', ...frontendScope], FRONTEND));

// ── Contract ────────────────────────────────────────────────────────────────
gate('contract:types', touches('contracts', 'frontend/src/shared/api'), () => {
  const dir = mkdtempSync(path.join(tmpdir(), 'homebase-api-'));
  try {
    const out = path.join(dir, 'schema.ts');
    const gen = npx('openapi-typescript', ['../contracts/openapi.yaml', '-o', out], FRONTEND);
    if (!gen.ok) return gen;
    const committed = readFileSync(path.join(FRONTEND, 'src/shared/api/generated/schema.ts'), 'utf8');
    const fresh = readFileSync(out, 'utf8');
    return committed === fresh
      ? { ok: true, output: 'generated types up to date', ms: gen.ms }
      : { ok: false, output: 'frontend/src/shared/api/generated/schema.ts is stale. Run: npm run gen:api', ms: gen.ms };
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});
gate('contract:routes', touches('contracts', 'backend'), () => run('node', ['scripts/check-api-contract.mjs']));

// ── Backend (.NET 10, ASP.NET Core, SQLite — every test runs on any OS) ─────
const dotnet = touches('backend', 'database', 'contracts') ? findDotnet() : null;
const needDotnet = (fn) => () => (dotnet ? fn() : { ok: true, skipped: true, output: '.NET SDK not found (install it or set DOTNET_ROOT) — backend NOT verified', ms: 0 });
gate('backend:build', touches('backend', 'database', 'contracts'), needDotnet(() => run(dotnet, ['build', 'backend/Homebase.sln', '-c', 'Release', '-nologo', '-clp:ErrorsOnly;Summary'])));
const backendTests = [
  ['domain', 'Homebase.Domain.Tests', ['backend']],
  ['devices', 'Homebase.Devices.Tests', ['backend']],
  ['application', 'Homebase.Application.Tests', ['backend']],
  ['data', 'Homebase.Data.Tests', ['backend', 'database']],
  ['api', 'Homebase.Api.Tests', ['backend', 'contracts']],
];
for (const [name, project, paths] of backendTests) {
  gate(`backend:test:${name}`, touches(...paths), needDotnet(() => run(dotnet, ['test', `backend/tests/${project}`, '-c', 'Release', '--no-build'])));
}

// ── Repo-wide ───────────────────────────────────────────────────────────────
gate('codemap', true, () => run('node', ['scripts/codemap.mjs', '--check']));
gate('ai-config', touches('.github', 'docs', 'AGENTS.md', 'scripts') || scopes.some((s) => s.endsWith('.instructions.md')), () => run('node', ['scripts/lint-ai-config.mjs', ...(strict ? ['--strict'] : [])]));
gate('scripts:test', touches('scripts', '.github/hooks'), () => run('node', ['--test', 'scripts/**/*.test.mjs']));

// ── Run ─────────────────────────────────────────────────────────────────────
console.log(`verify${scopes.length ? ` (scope: ${scopes.join(', ')})` : ''}${strict ? ' --strict' : ''}`);
let failed = 0;
let skipped = 0;
let gaps = 0; // skips caused by a missing tool/env, not by the OS — these fail --strict
for (const { name, fn } of gates) {
  const r = fn();
  const secs = `${(r.ms / 1000).toFixed(1)}s`.padStart(6);
  if (r.skipped) {
    skipped += 1;
    if (!r.platform) gaps += 1;
    console.log(`  ○ ${name.padEnd(22)}${secs}  SKIPPED — ${r.output.trim().split('\n').pop()}`);
  } else if (r.ok) {
    console.log(`  ✔ ${name.padEnd(22)}${secs}  ${summarize(name, r.output)}`.trimEnd());
  } else {
    failed += 1;
    console.log(`  ✖ ${name.padEnd(22)}${secs}`);
    const tail = r.output.trim().split('\n').filter(Boolean).slice(-TAIL_LINES);
    for (const line of tail) console.log(`      ${line}`);
  }
}

const passed = gates.length - failed - skipped;
const strictFail = strict && gaps > 0;
if (failed || strictFail) {
  console.log(`VERIFY FAILED — ${failed} failed, ${skipped} skipped, ${passed} passed`);
  process.exit(1);
}
console.log(skipped ? `VERIFY PASSED WITH GAPS — ${passed} passed, ${skipped} skipped (state the gaps in your report)` : `VERIFY PASSED — ${passed}/${gates.length} gates`);
if (!existsSync(path.join(ROOT, 'node_modules'))) console.log('note: run `npm ci` first');

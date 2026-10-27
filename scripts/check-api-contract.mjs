#!/usr/bin/env node
// Fails when ASP.NET Core controller routes and contracts/openapi.yaml disagree, in either direction.
// Runs without .NET, so agents get feedback on backend routes before (or without) a build.
//
// Relies on the route convention in .github/instructions/backend-dotnet.instructions.md:
//   [Route("api/devices")] on its own line above the controller class, and on every action
//   [HttpGet("...")] / [HttpPost("...")] / [HttpPut("...")] / [HttpDelete("...")] on its own line (template relative to the class).
import { readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { parse } from 'yaml';

const ROOT = path.resolve(import.meta.dirname, '..');
const CONTROLLERS = path.join(ROOT, 'backend/src/Homebase.Api/Controllers');
const METHODS = ['get', 'post', 'put', 'patch', 'delete'];

const spec = parse(readFileSync(path.join(ROOT, 'contracts/openapi.yaml'), 'utf8'));
const contract = new Set(
  Object.entries(spec.paths).flatMap(([p, ops]) => Object.keys(ops).filter((m) => METHODS.includes(m)).map((m) => `${m.toUpperCase()} ${p}`)),
);

const implemented = new Map();
const problems = [];
let files = [];
try {
  files = readdirSync(CONTROLLERS).filter((f) => f.endsWith('.cs'));
} catch {
  console.error(`No controllers found at ${path.relative(ROOT, CONTROLLERS)}`);
  process.exit(1);
}

for (const file of files) {
  const text = readFileSync(path.join(CONTROLLERS, file), 'utf8');
  const head = text.slice(0, text.search(/\bclass\s/) === -1 ? text.length : text.search(/\bclass\s/));
  const prefix = head.match(/^\s*\[Route\("([^"]*)"\)\]\s*$/m)?.[1];
  if (prefix === undefined) {
    if (/\[Http(Get|Post|Put|Patch|Delete)\b/.test(text)) problems.push(`${file}: has actions but no [Route("...")] above the class`);
    continue;
  }
  const lines = text.split('\n');
  for (let i = 0; i < lines.length; i += 1) {
    const line = lines[i].trim();
    const m = line.match(/^\[Http(Get|Post|Put|Patch|Delete)(?:\("([^"]*)"\))?\]$/);
    if (!m) {
      if (/^\[Http(Get|Post|Put|Patch|Delete)\b/.test(line)) problems.push(`${file}:${i + 1}: put the route template inside the verb attribute on its own line, e.g. [HttpGet("{id}")]`);
      continue;
    }
    if (m[2]?.startsWith('/') || m[2]?.startsWith('~')) problems.push(`${file}:${i + 1}: absolute route templates bypass the class [Route]; keep them relative`);
    const full = `/${[prefix, m[2]].filter(Boolean).join('/')}`.replace(/\/+/g, '/').replace(/\/$/, '');
    implemented.set(`${m[1].toUpperCase()} ${full}`, `${file}:${i + 1}`);
  }
}

for (const op of contract) if (!implemented.has(op)) problems.push(`missing in backend: ${op}`);
for (const [op, where] of implemented) if (!contract.has(op)) problems.push(`not in contract: ${op} (${where}) — add it to contracts/openapi.yaml or remove it`);

if (problems.length) {
  console.error(problems.join('\n'));
  console.error(`contract check: ${problems.length} error(s)`);
  process.exit(1);
}
console.log(`contract check: ok — ${contract.size} operations match ${files.length} controllers`);

// Policy for the Copilot preToolUse hook (.github/hooks/guardrails.json).
// Pure function so it is unit-tested (guardrails.test.mjs). Keep rules few, specific and
// explained: the agent sees `reason` and should be able to do the right thing next.
import path from 'node:path';

/** Paths no agent may write. `reason` tells the agent what to do instead. */
export const PROTECTED_PATHS = [
  { pattern: /^docs\/CODEMAP\.md$/, reason: 'docs/CODEMAP.md is generated. Run `npm run codemap` instead of editing it.' },
  { pattern: /^frontend\/src\/shared\/api\/generated\//, reason: 'Generated from contracts/openapi.yaml. Edit the contract, then run `npm run gen:api`.' },
  { pattern: /(^|\/)package-lock\.json$/, reason: 'Do not hand-edit the lockfile. Use `npm install <pkg>@<exact-version>`.' },
  { pattern: /^database\/migrations\/0001_|^database\/migrations\/0002_/, reason: 'Shipped migrations are immutable (DbUp will not re-run them). Add a new numbered migration instead.' },
  { pattern: /^(\.github\/hooks|scripts\/hooks)\//, reason: 'Agents must not modify their own guardrails. Ask the maintainer to change them.' },
  { pattern: /(^|\/)\.env(\.|$)/, reason: 'Environment files may contain secrets; agents must not touch them.' },
];

/** Shell commands no agent may run. */
export const BLOCKED_COMMANDS = [
  { pattern: /\bgit\s+push\b[^\n;&|]*\s(--force\b|-f\b|--force-with-lease\b)/, reason: 'Force-pushing rewrites shared history. Push normally or ask a human.' },
  { pattern: /\bgit\s+push\b[^\n;&|]*\b(origin\s+)?(main|master)\b/, reason: 'Never push to the default branch; open a pull request.' },
  { pattern: /--no-verify\b/, reason: 'Skipping git hooks hides failures. Fix the failure instead.' },
  { pattern: /\bgit\s+reset\s+--hard\b/, reason: 'Hard resets destroy work. Use `git stash` or ask a human.' },
  { pattern: /\b(npm|dotnet\s+nuget)\s+publish\b|\bdotnet\s+nuget\s+push\b/, reason: 'Publishing packages is a human-only action.' },
  { pattern: /\b(curl|wget)\b[^\n|]*\|\s*(sudo\s+)?(ba|z)?sh\b/, reason: 'Piping remote scripts into a shell is blocked.' },
  { pattern: /\bDROP\s+(DATABASE|TABLE)\b|\bTRUNCATE\s+TABLE\b/i, reason: 'Destructive SQL is blocked. Write a migration and let a human review it.' },
  { pattern: /(^|[\s;&|(])(cat|less|more|head|tail|source|type)\s+[^\n;&|]*\.env\b/, reason: 'Agents must not read .env files.' },
];

const READ_TOOL = /^(view|read|read_?file|cat|grep|glob|search|ls|list)/i;
const WRITE_TOOL = /(edit|write|create|replace|patch|delete|move|rename|insert)/i;
const SHELL_TOOL = /^(bash|shell|powershell|execute|run_?in_?terminal|terminal)$/i;
const PATH_KEYS = ['path', 'file_path', 'filePath', 'filename', 'target_file', 'file', 'paths'];

function normalize(p, cwd, repoRoot) {
  const absolute = path.resolve(cwd || repoRoot, String(p));
  return path.relative(repoRoot, absolute).split(path.sep).join('/');
}

function collectPaths(args) {
  return PATH_KEYS.flatMap((k) => (Array.isArray(args?.[k]) ? args[k] : args?.[k] ? [args[k]] : []));
}

function rmTargetsDanger(command) {
  for (const segment of command.split(/[;&|]+/)) {
    const tokens = segment.trim().split(/\s+/);
    if (tokens[0] !== 'rm' && tokens[0] !== 'sudo') continue;
    const words = tokens[0] === 'sudo' ? tokens.slice(1) : tokens;
    if (words[0] !== 'rm') continue;
    const recursive = words.some((w) => /^-[a-zA-Z]*r/.test(w) || w === '--recursive');
    const targets = words.slice(1).filter((w) => !w.startsWith('-'));
    if (recursive && targets.some((t) => ['/', '/*', '~', '~/', '.', './', '..', '*', '$HOME'].includes(t))) return true;
  }
  return false;
}

/** Normalises the camelCase (CLI/cloud agent) and snake_case (VS Code-compatible) payloads. */
export function readPayload(raw) {
  const payload = typeof raw === 'string' ? JSON.parse(raw) : raw;
  const toolName = payload.toolName ?? payload.tool_name ?? '';
  let toolArgs = payload.toolArgs ?? payload.tool_input ?? {};
  if (typeof toolArgs === 'string') {
    try {
      toolArgs = JSON.parse(toolArgs);
    } catch {
      toolArgs = { command: toolArgs };
    }
  }
  return { toolName, toolArgs, cwd: payload.cwd };
}

/** Returns a deny decision, or null for "no opinion" (normal permission flow continues). */
export function decide({ toolName, toolArgs, cwd }, repoRoot) {
  const deny = (reason) => ({ permissionDecision: 'deny', permissionDecisionReason: `Blocked by repo guardrail: ${reason}` });

  if (SHELL_TOOL.test(toolName)) {
    const command = String(toolArgs.command ?? toolArgs.cmd ?? toolArgs.script ?? '');
    const hit = BLOCKED_COMMANDS.find((r) => r.pattern.test(command));
    if (hit) return deny(hit.reason);
    if (rmTargetsDanger(command)) return deny('Recursive delete of the root, home or whole repo is blocked.');
    for (const m of command.matchAll(/(?:>>?|\btee\s+(?:-a\s+)?)\s*([^\s;&|]+)/g)) {
      const target = normalize(m[1], cwd, repoRoot);
      const rule = PROTECTED_PATHS.find((r) => r.pattern.test(target));
      if (rule) return deny(rule.reason);
    }
    return null;
  }

  const paths = collectPaths(toolArgs).map((p) => normalize(p, cwd, repoRoot));
  if (READ_TOOL.test(toolName)) {
    // Reading is fine except secrets.
    const secret = paths.find((p) => /(^|\/)\.env(\.|$)/.test(p));
    return secret ? deny('Environment files may contain secrets; agents must not read them.') : null;
  }
  if (WRITE_TOOL.test(toolName)) {
    for (const p of paths) {
      const rule = PROTECTED_PATHS.find((r) => r.pattern.test(p));
      if (rule) return deny(rule.reason);
    }
  }
  return null;
}

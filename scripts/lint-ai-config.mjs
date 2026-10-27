#!/usr/bin/env node
// Lints the AI context layer (instructions, agents, prompts, skills, hooks) the way ESLint lints code.
// Stale or bloated agent context is a top cause of confident-but-wrong agent output, and every
// always-on token is paid on every request. `--strict` turns warnings into errors (weekly CI job).
//
// When GitHub or VS Code rename/retire a field, add it to DEPRECATIONS below — that is how this
// repo "stays current" (docs/ai/STAYING-CURRENT.md).
import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs';
import path from 'node:path';
import { parse as parseYaml } from 'yaml';

const ROOT = path.resolve(import.meta.dirname, '..');
const strict = process.argv.includes('--strict');
const errors = [];
const warnings = [];
const rel = (p) => path.relative(ROOT, p).split(path.sep).join('/');
const approxTokens = (text) => Math.ceil(text.length / 4);

// ── Budgets ─────────────────────────────────────────────────────────────────
const ALWAYS_ON_BUDGET = 1500; // .github/copilot-instructions.md + root AGENTS.md, paid on every request
const SCOPED_BUDGET = 900; // each .instructions.md, paid whenever its applyTo matches
const AGENT_BODY_MAX_CHARS = 30_000; // Copilot cloud agent limit for custom agent prompts
const SKILL_DESCRIPTION_MAX = 1024;
const MODEL_REVERIFY_DAYS = 90;

// ── Known schema (verified 2026-10-26; see docs/ai/STAYING-CURRENT.md) ──────
const KNOWN_FIELDS = {
  instruction: ['applyTo', 'description', 'name', 'excludeAgent'],
  agent: ['name', 'description', 'tools', 'model', 'handoffs', 'target', 'agents', 'user-invocable', 'disable-model-invocation', 'argument-hint', 'mcp-servers', 'reasoning-effort', 'hooks', 'metadata'],
  prompt: ['name', 'description', 'argument-hint', 'agent', 'model', 'tools'],
  skill: ['name', 'description', 'license', 'compatibility', 'metadata', 'allowed-tools'],
};
const DEPRECATIONS = [
  { kind: 'agent', field: 'infer', message: '`infer` is retired — use `user-invocable` and `disable-model-invocation`.' },
  { kind: 'prompt', field: 'mode', message: '`mode` was renamed to `agent` in prompt files.' },
];
const EXCLUDE_AGENT_VALUES = ['code-review', 'cloud-agent'];
const RENAMED_VALUES = { 'coding-agent': 'cloud-agent' };
const BUILTIN_AGENTS = ['agent', 'ask', 'plan'];
const TOOL_ALIASES = ['execute', 'read', 'edit', 'search', 'agent', 'web', 'todo'];
const INTENTIONALLY_ABSENT = ['.github/chatmodes']; // retired locations named in docs/ai/STAYING-CURRENT.md
const HOOK_EVENTS = ['sessionStart', 'sessionEnd', 'userPromptSubmitted', 'userPromptTransformed', 'preToolUse', 'permissionRequest', 'postToolUse', 'postToolUseFailure', 'agentStop', 'subagentStart', 'subagentStop', 'preCompact', 'errorOccurred', 'notification'];

// ── Helpers ─────────────────────────────────────────────────────────────────
function listFiles(dir, accept) {
  if (!existsSync(dir)) return [];
  return readdirSync(dir).flatMap((name) => {
    const full = path.join(dir, name);
    if (['node_modules', 'bin', 'obj', 'dist', '.git'].includes(name)) return [];
    return statSync(full).isDirectory() ? listFiles(full, accept) : accept(full) ? [full] : [];
  });
}

function frontmatter(file) {
  const text = readFileSync(file, 'utf8');
  const m = text.match(/^---\n([\s\S]*?)\n---\n?([\s\S]*)$/);
  if (!m) return { data: null, body: text, text };
  try {
    return { data: parseYaml(m[1]) ?? {}, body: m[2], text };
  } catch (e) {
    errors.push(`${rel(file)}: invalid YAML frontmatter — ${e.message.split('\n')[0]}`);
    return { data: {}, body: m[2], text };
  }
}

function checkFields(file, kind, data) {
  for (const key of Object.keys(data)) {
    const deprecated = DEPRECATIONS.find((d) => d.kind === kind && d.field === key);
    if (deprecated) errors.push(`${rel(file)}: ${deprecated.message}`);
    else if (!KNOWN_FIELDS[kind].includes(key)) warnings.push(`${rel(file)}: unknown ${kind} field \`${key}\` — typo, or a new field to add to KNOWN_FIELDS?`);
  }
}

const repoFiles = listFiles(ROOT, () => true).map(rel);

function checkGlobs(file, applyTo) {
  for (const glob of String(applyTo).split(',').map((g) => g.trim()).filter(Boolean)) {
    if (!repoFiles.some((f) => path.matchesGlob(f, glob))) {
      errors.push(`${rel(file)}: applyTo "${glob}" matches no files — stale after a move/rename?`);
    }
  }
}

function checkLinks(file, text) {
  const dir = path.dirname(file);
  for (const m of text.matchAll(/\]\(([^)\s]+)\)/g)) {
    const target = m[1].split('#')[0];
    if (!target || /^[a-z]+:/i.test(target)) continue;
    if (!existsSync(path.resolve(dir, decodeURI(target)))) errors.push(`${rel(file)}: broken link → ${m[1]}`);
  }
  // Backticked repo paths are followed by agents too, so they must exist.
  // Placeholders (NNNN) and retired paths that docs mention on purpose are exempt.
  for (const m of text.matchAll(/`((?:frontend|backend|database|contracts|scripts|docs|\.github)\/[^`\s*<>{}]+)`/g)) {
    const target = m[1].replace(/[:#].*$/, '').replace(/\/$/, '');
    if (/NNNN/.test(target) || INTENTIONALLY_ABSENT.includes(target)) continue;
    if (!existsSync(path.join(ROOT, target))) errors.push(`${rel(file)}: references missing path \`${m[1]}\``);
  }
}

// ── Models ──────────────────────────────────────────────────────────────────
const modelsFile = path.join(ROOT, '.github/ai-models.json');
const models = JSON.parse(readFileSync(modelsFile, 'utf8'));
const approvedModels = new Set(Object.values(models.tiers).flat());
const ageDays = Math.floor((Date.now() - Date.parse(models.lastVerified)) / 86_400_000);
if (ageDays > MODEL_REVERIFY_DAYS) {
  warnings.push(`.github/ai-models.json: model list last verified ${ageDays} days ago — re-check ${models.source} and update lastVerified`);
}
function checkModels(file, value) {
  for (const model of [value].flat().filter(Boolean)) {
    if (!approvedModels.has(model)) errors.push(`${rel(file)}: model "${model}" is not in .github/ai-models.json (unapproved, misspelled or retired)`);
  }
}

// ── Always-on context ───────────────────────────────────────────────────────
const alwaysOn = ['.github/copilot-instructions.md', 'AGENTS.md'].map((f) => path.join(ROOT, f));
let alwaysOnTokens = 0;
for (const file of alwaysOn) {
  if (!existsSync(file)) {
    errors.push(`${rel(file)}: missing`);
    continue;
  }
  const text = readFileSync(file, 'utf8');
  alwaysOnTokens += approxTokens(text);
  checkLinks(file, text);
}
if (alwaysOnTokens > ALWAYS_ON_BUDGET) {
  errors.push(`always-on context is ~${alwaysOnTokens} tokens (budget ${ALWAYS_ON_BUDGET}). Move detail into scoped .instructions.md, skills or docs.`);
}

// ── Path-scoped instructions ────────────────────────────────────────────────
for (const file of listFiles(path.join(ROOT, '.github/instructions'), (f) => f.endsWith('.instructions.md'))) {
  const { data, text } = frontmatter(file);
  if (!data) {
    errors.push(`${rel(file)}: needs frontmatter with applyTo or description`);
    continue;
  }
  checkFields(file, 'instruction', data);
  if (!data.applyTo && !data.description) errors.push(`${rel(file)}: needs applyTo (auto-attach) or description (on-demand)`);
  if (data.applyTo) checkGlobs(file, data.applyTo);
  if (data.excludeAgent) {
    const v = data.excludeAgent;
    if (RENAMED_VALUES[v]) errors.push(`${rel(file)}: excludeAgent "${v}" is now "${RENAMED_VALUES[v]}"`);
    else if (!EXCLUDE_AGENT_VALUES.includes(v)) errors.push(`${rel(file)}: excludeAgent must be one of ${EXCLUDE_AGENT_VALUES.join(', ')}`);
  }
  const tokens = approxTokens(text);
  if (tokens > SCOPED_BUDGET) warnings.push(`${rel(file)}: ~${tokens} tokens (budget ${SCOPED_BUDGET}) — split it or move reference material to a skill`);
  checkLinks(file, text);
}

// ── Custom agents ───────────────────────────────────────────────────────────
const agentFiles = listFiles(path.join(ROOT, '.github/agents'), (f) => f.endsWith('.agent.md'));
const agentNames = new Set(agentFiles.map((f) => frontmatter(f).data?.name ?? path.basename(f, '.agent.md')));
for (const file of agentFiles) {
  const { data, body, text } = frontmatter(file);
  if (!data) {
    errors.push(`${rel(file)}: missing frontmatter`);
    continue;
  }
  checkFields(file, 'agent', data);
  if (!data.description) errors.push(`${rel(file)}: description is required`);
  if (!data.tools) errors.push(`${rel(file)}: declare tools explicitly (least privilege; omitting grants every tool)`);
  for (const tool of [data.tools ?? []].flat()) {
    if (!TOOL_ALIASES.includes(tool) && !String(tool).includes('/')) warnings.push(`${rel(file)}: unknown tool "${tool}" — portable aliases are ${TOOL_ALIASES.join(', ')}`);
  }
  checkModels(file, data.model);
  for (const h of data.handoffs ?? []) {
    if (!agentNames.has(h.agent) && !BUILTIN_AGENTS.includes(h.agent)) errors.push(`${rel(file)}: handoff to unknown agent "${h.agent}"`);
    checkModels(file, h.model);
  }
  if (body.length > AGENT_BODY_MAX_CHARS) errors.push(`${rel(file)}: body is ${body.length} chars (cloud agent max ${AGENT_BODY_MAX_CHARS})`);
  checkLinks(file, text);
}

// ── Prompt files ────────────────────────────────────────────────────────────
for (const file of listFiles(path.join(ROOT, '.github/prompts'), (f) => f.endsWith('.prompt.md'))) {
  const { data, text } = frontmatter(file);
  if (!data?.description) errors.push(`${rel(file)}: description is required`);
  if (data) {
    checkFields(file, 'prompt', data);
    if (data.agent && !agentNames.has(data.agent) && !BUILTIN_AGENTS.includes(data.agent)) errors.push(`${rel(file)}: unknown agent "${data.agent}"`);
    checkModels(file, data.model);
  }
  checkLinks(file, text);
}

// ── Skills ──────────────────────────────────────────────────────────────────
const skillsDir = path.join(ROOT, '.github/skills');
for (const dir of existsSync(skillsDir) ? readdirSync(skillsDir) : []) {
  const file = path.join(skillsDir, dir, 'SKILL.md');
  if (!existsSync(file)) {
    errors.push(`.github/skills/${dir}: missing SKILL.md (exact name required)`);
    continue;
  }
  const { data, text } = frontmatter(file);
  if (!data) {
    errors.push(`${rel(file)}: missing frontmatter`);
    continue;
  }
  checkFields(file, 'skill', data);
  if (data.name !== dir) errors.push(`${rel(file)}: name "${data.name}" must match its directory "${dir}"`);
  if (!/^[a-z0-9]+(-[a-z0-9]+)*$/.test(data.name ?? '')) errors.push(`${rel(file)}: name must be lowercase-hyphenated`);
  if (!data.description) errors.push(`${rel(file)}: description is required (it is how the agent decides to load the skill)`);
  else if (data.description.length > SKILL_DESCRIPTION_MAX) errors.push(`${rel(file)}: description over ${SKILL_DESCRIPTION_MAX} chars`);
  checkLinks(file, text);
}

// ── Hooks ───────────────────────────────────────────────────────────────────
for (const file of listFiles(path.join(ROOT, '.github/hooks'), (f) => f.endsWith('.json'))) {
  let config;
  try {
    config = JSON.parse(readFileSync(file, 'utf8'));
  } catch (e) {
    errors.push(`${rel(file)}: invalid JSON — ${e.message}`);
    continue;
  }
  if (config.version !== 1) errors.push(`${rel(file)}: version must be 1`);
  for (const [event, entries] of Object.entries(config.hooks ?? {})) {
    const camel = event.charAt(0).toLowerCase() + event.slice(1);
    if (!HOOK_EVENTS.includes(camel)) errors.push(`${rel(file)}: unknown hook event "${event}"`);
    for (const entry of entries) {
      const command = entry.bash ?? entry.command;
      if (!command) errors.push(`${rel(file)}: ${event} entry needs "bash" (cloud agent honours only bash/command)`);
      const script = command?.match(/node\s+(\S+\.m?js)/)?.[1];
      if (script && !existsSync(path.join(ROOT, script))) errors.push(`${rel(file)}: hook script ${script} does not exist`);
    }
  }
}

// ── Retired locations ───────────────────────────────────────────────────────
if (existsSync(path.join(ROOT, '.github/chatmodes'))) errors.push('.github/chatmodes/ is retired — chat modes are now custom agents in .github/agents/*.agent.md');

// ── Docs referenced by agents ───────────────────────────────────────────────
for (const file of listFiles(path.join(ROOT, 'docs'), (f) => f.endsWith('.md'))) checkLinks(file, readFileSync(file, 'utf8'));
checkLinks(path.join(ROOT, 'README.md'), readFileSync(path.join(ROOT, 'README.md'), 'utf8'));

// ── Report ──────────────────────────────────────────────────────────────────
for (const e of errors) console.log(`error   ${e}`);
for (const w of warnings) console.log(`warning ${w}`);
const failing = errors.length + (strict ? warnings.length : 0);
console.log(`ai-config: ${errors.length} errors, ${warnings.length} warnings · always-on context ~${alwaysOnTokens}/${ALWAYS_ON_BUDGET} tokens`);
process.exit(failing ? 1 : 0);

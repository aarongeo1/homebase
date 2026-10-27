#!/usr/bin/env node
// Builds the frontend and runs Homebase in the foreground on http://localhost:5080 (Ctrl+C to stop).
// Settings are environment variables (docs/ARCHITECTURE.md "Configuration"), e.g.
//   HOMEBASE_LATITUDE=51.51 HOMEBASE_LONGITUDE=-0.13 npm start
import { spawnSync } from 'node:child_process';
import path from 'node:path';
import { findDotnet } from './dotnet.mjs';

const ROOT = path.resolve(import.meta.dirname, '..');
const dotnet = findDotnet();
if (!dotnet) {
  console.error('.NET SDK 10 not found. Install it (https://dot.net) or set DOTNET_ROOT.');
  process.exit(1);
}

const build = spawnSync('npm', ['run', 'build'], { cwd: ROOT, stdio: 'inherit', shell: process.platform === 'win32' });
if (build.status !== 0) process.exit(build.status ?? 1);

const run = spawnSync(dotnet, ['run', '--project', 'backend/src/Homebase.Api.Host', '-c', 'Release'], {
  cwd: ROOT,
  stdio: 'inherit',
  env: { ...process.env, HOMEBASE_WEB_ROOT: process.env.HOMEBASE_WEB_ROOT ?? path.join(ROOT, 'frontend', 'dist'), DOTNET_NOLOGO: '1' },
});
process.exit(run.status ?? 0);

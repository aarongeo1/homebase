#!/usr/bin/env node
// Runs Homebase in the background on the Mac, starting at login and restarting if it stops (a launchd LaunchAgent).
//
//   HOMEBASE_LATITUDE=51.51 HOMEBASE_LONGITUDE=-0.13 npm run mac:install   # build, copy, (re)start
//   npm run mac:status
//   npm run mac:uninstall                                                     # stop and remove (keeps your data)
//
// install copies the built app to ~/Library/Application Support/Homebase/app and bakes the HOMEBASE_* variables
// present at install time into the agent. Re-run it after changing code or settings. Logs: .../Homebase/homebase.log.
import { spawnSync } from 'node:child_process';
import { cpSync, existsSync, mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { homedir, userInfo } from 'node:os';
import path from 'node:path';
import { findDotnet } from './dotnet.mjs';

const ROOT = path.resolve(import.meta.dirname, '..');
const LABEL = 'com.homebase.app';
const DATA = process.env.HOMEBASE_DATA ?? path.join(homedir(), 'Library', 'Application Support', 'Homebase');
const APP = path.join(DATA, 'app');
const PLIST = path.join(homedir(), 'Library', 'LaunchAgents', `${LABEL}.plist`);
const DOMAIN = `gui/${userInfo().uid}`;
const command = process.argv[2];

if (process.platform !== 'darwin') {
  console.error('This script sets up a macOS LaunchAgent. On other systems run `npm start`.');
  process.exit(1);
}

const sh = (cmd, args, opts = {}) => spawnSync(cmd, args, { cwd: ROOT, encoding: 'utf8', ...opts });
const step = (label, result) => {
  if (result.status !== 0) {
    console.error(`${label} failed:\n${(result.stdout ?? '') + (result.stderr ?? '')}`.trim());
    process.exit(1);
  }
};
const xml = (s) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
const stop = () => sh('launchctl', ['bootout', `${DOMAIN}/${LABEL}`]);

if (command === 'install') {
  const dotnet = findDotnet();
  if (!dotnet) {
    console.error('.NET SDK 10 not found. Install it (https://dot.net) or set DOTNET_ROOT.');
    process.exit(1);
  }

  console.log('Building the frontend…');
  step('npm run build', sh('npm', ['run', 'build']));
  console.log('Publishing the backend…');
  rmSync(APP, { recursive: true, force: true });
  mkdirSync(APP, { recursive: true });
  step('dotnet publish', sh(dotnet, ['publish', 'backend/src/Homebase.Api.Host', '-c', 'Release', '-o', APP, '-nologo']));
  cpSync(path.join(ROOT, 'frontend', 'dist'), path.join(APP, 'wwwroot'), { recursive: true });

  const env = {
    ...Object.fromEntries(Object.entries(process.env).filter(([k]) => k.startsWith('HOMEBASE_'))),
    HOMEBASE_DATA: DATA,
    HOMEBASE_WEB_ROOT: path.join(APP, 'wwwroot'),
    DOTNET_ROOT: path.dirname(dotnet === 'dotnet' ? sh('which', ['dotnet']).stdout.trim() : dotnet),
  };
  const log = path.join(DATA, 'homebase.log');
  writeFileSync(PLIST, `<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>Label</key><string>${LABEL}</string>
  <key>ProgramArguments</key>
  <array><string>${xml(dotnet)}</string><string>${xml(path.join(APP, 'Homebase.dll'))}</string></array>
  <key>WorkingDirectory</key><string>${xml(APP)}</string>
  <key>EnvironmentVariables</key>
  <dict>
${Object.entries(env).map(([k, v]) => `    <key>${xml(k)}</key><string>${xml(v)}</string>`).join('\n')}
  </dict>
  <key>RunAtLoad</key><true/>
  <key>KeepAlive</key><true/>
  <key>StandardOutPath</key><string>${xml(log)}</string>
  <key>StandardErrorPath</key><string>${xml(log)}</string>
</dict>
</plist>
`);

  stop();
  step('launchctl bootstrap', sh('launchctl', ['bootstrap', DOMAIN, PLIST]));
  console.log(`Homebase is running and will start at login: http://localhost:5080\nLogs: ${log}`);
  console.log('If macOS asks whether "dotnet" may find devices on your local network, allow it (Govee and the TV need it).');
} else if (command === 'uninstall') {
  stop();
  rmSync(PLIST, { force: true });
  rmSync(APP, { recursive: true, force: true });
  console.log(`Stopped and removed. Your data is still in ${DATA}.`);
} else if (command === 'status') {
  const r = sh('launchctl', ['print', `${DOMAIN}/${LABEL}`]);
  if (r.status !== 0) {
    console.log(existsSync(PLIST) ? 'Installed but not loaded. Run `npm run mac:install`.' : 'Not installed.');
  } else {
    const state = r.stdout.match(/^\s*state = (.+)$/m)?.[1] ?? 'unknown';
    const pid = r.stdout.match(/^\s*pid = (\d+)$/m)?.[1];
    console.log(`Homebase: ${state}${pid ? ` (pid ${pid})` : ''} — http://localhost:5080`);
  }
} else {
  console.error('Usage: node scripts/macos-service.mjs install|uninstall|status');
  process.exit(2);
}

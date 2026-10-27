// Finds the .NET SDK the same way everywhere: DOTNET_ROOT, then ~/.dotnet (the official install script's default), then PATH.
import { spawnSync } from 'node:child_process';
import { homedir } from 'node:os';
import path from 'node:path';

export function findDotnet() {
  const exe = process.platform === 'win32' ? 'dotnet.exe' : 'dotnet';
  const candidates = [process.env.DOTNET_ROOT && path.join(process.env.DOTNET_ROOT, exe), path.join(homedir(), '.dotnet', exe), 'dotnet'];
  for (const c of candidates.filter(Boolean)) {
    if (spawnSync(c, ['--version'], { encoding: 'utf8' }).status === 0) return c;
  }
  return null;
}

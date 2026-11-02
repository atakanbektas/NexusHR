import { spawnSync } from 'node:child_process';
import { existsSync, realpathSync, statSync } from 'node:fs';
import { resolve, sep } from 'node:path';
const services = ['CANDIDATE', 'HIRING', 'ONBOARDING', 'NOTIFICATION', 'GATEWAY', 'WEB'];
for (const service of services) {
  if (!/^.+@sha256:[a-f0-9]{64}$/.test(process.env[`${service}_IMAGE`] ?? '')) throw new Error(`${service}_IMAGE must identify a reviewed immutable digest.`);
  if (service !== 'WEB') {
    const file = resolve(process.env[`${service}_ENV_FILE`] ?? '.');
    if (!existsSync(file) || !statSync(file).isFile()) throw new Error(`${service}_ENV_FILE must name a private file outside the repository.`);
    const canonical = value => process.platform === 'win32' ? realpathSync(value).toLowerCase() : realpathSync(value);
    const actual = canonical(file), root = canonical(process.cwd());
    if (actual === root || actual.startsWith(root + sep)) throw new Error(`${service}_ENV_FILE must resolve outside the repository.`);
    if (process.platform !== 'win32' && (statSync(file).mode & 0o077) !== 0) throw new Error(`${service}_ENV_FILE must be private (mode 0600 or stricter).`);
  }
}
const result = spawnSync(process.env.DOCKER_EXE ?? 'docker', ['compose', '-f', 'deploy/staging/compose.yml', 'config', '--quiet'], { encoding: 'utf8' });
if (result.error) throw new Error('Docker Compose is unavailable.');
process.exitCode = result.status ?? 1;
if (process.exitCode) console.error('Staging validation failed. Inspect configuration privately; resolved settings and parser details are deliberately omitted.');

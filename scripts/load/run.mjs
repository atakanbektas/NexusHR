// Real Keycloak login; API placeholders only avoid reading business data during login.
// All measured requests go to the separate real load fixture, never the UI placeholders.
import { chromium } from '../../NexusHR.Web/node_modules/@playwright/test/index.mjs';
import { spawn } from 'node:child_process';
import { mkdir } from 'node:fs/promises';
import { resolve } from 'node:path';

async function run() {
  const output = process.argv[2];
  if (!/^\.tools\/load-[a-z-]+$/.test(output ?? '')) throw new Error('Use .tools/load-<name> for metrics only.');
  if (!process.env.E2E_USERNAME || !process.env.E2E_PASSWORD) throw new Error('Supply an authorized synthetic-development account through E2E_USERNAME/E2E_PASSWORD.');
  await mkdir(output, { recursive: true });
  const browser = await chromium.launch({ headless: true });
  try {
    const context = await browser.newContext(); const page = await context.newPage(); let token = '';
    await page.route('http://localhost:5000/api/**', async route => {
      const authorization = route.request().headers().authorization; if (authorization) token = authorization;
      await route.fulfill({ json: { items: [], page: 1, pageSize: 20, totalCount: 0 } });
    });
    await page.goto('http://localhost:5173'); await page.getByRole('button', { name: 'Personel girişi', exact: true }).click();
    await page.locator('#username').fill(process.env.E2E_USERNAME); await page.locator('#password').fill(process.env.E2E_PASSWORD); await page.locator('#kc-login').click();
    for (let i = 0; i < 100 && !token; i++) await new Promise(resolve => setTimeout(resolve, 100));
    if (!token) throw new Error('Authorized login did not produce a token.');
    const child = spawn(process.env.DOCKER_EXE ?? 'docker', ['run', '--rm', '--user', '0', '--add-host', 'host.docker.internal:host-gateway',
      '-v', `${resolve('scripts/load')}:/work:ro`, '-v', `${resolve(output)}:/results`, '-w', '/results',
      '-e', 'LOAD_BEARER', '-e', 'LOAD_BASE_URL', '-e', 'LOAD_SEED_COUNT', '-e', 'LOAD_SKIP_SEED', '-e', 'LOAD_DURATION',
      'grafana/k6@sha256:3e4186aea62e11a45560bfc51e19eeafe82fc07ce069c78aff44b7ff893d9a5e', 'run', '--quiet', '/work/scenarios.js'], {
      stdio: 'inherit', env: { ...process.env, LOAD_BEARER: token, LOAD_BASE_URL: 'http://host.docker.internal:5400',
        LOAD_SEED_COUNT: process.env.LOAD_SEED_COUNT ?? '4000', LOAD_SKIP_SEED: process.env.LOAD_SKIP_SEED ?? '0', LOAD_DURATION: process.env.LOAD_DURATION ?? '30s' }
    });
    const deadline = setTimeout(() => child.kill('SIGTERM'), 10 * 60 * 1000);
    try { process.exitCode = await new Promise((resolve, reject) => { child.on('exit', code => resolve(code ?? 1)); child.on('error', reject); }); }
    finally { clearTimeout(deadline); }
  } finally { await browser.close(); }
}
run().catch(() => { console.error('Load fixture login/execution failed. Check local service readiness and configuration; credentials and browser error details are deliberately omitted.'); process.exitCode = 1; });

// Bounded, read-only authenticated local smoke load. Never log tokens or response bodies.
import { pathToFileURL } from 'node:url';
export async function measureReadLoad({ baseUrl = 'http://localhost:5000', token, concurrency = 5, requests = 500, pacingMs = 100, p95BudgetMs = 1000 }) {
  const url = new URL(baseUrl);
  if (!['localhost', '127.0.0.1', '[::1]'].includes(url.hostname) || url.username || url.password || url.pathname !== '/' || url.search || url.hash)
    throw new Error('Only an explicit loopback development target is supported.');
  if (!token?.startsWith('Bearer ') || !Number.isInteger(concurrency) || concurrency < 1 || concurrency > 20 ||
      !Number.isInteger(requests) || requests < 10 || requests > 1000 || !Number.isInteger(pacingMs) || pacingMs < 0 || pacingMs > 500) throw new Error('Bearer token and bounded concurrency/request count required.');
  const target = new URL('/api/candidates?page=1&pageSize=20&search=__nexushr_load_probe__', url);
  const headers = { Authorization: token }; const times = []; const statuses = {}; let next = 0; let failed = 0;
  // Warm-up is explicit and excluded from reported measurements.
  for (let i = 0; i < 5; i++) { const response = await fetch(target, { headers, signal: AbortSignal.timeout(5000) }); await response.arrayBuffer(); if (!response.ok) throw new Error('Authenticated warm-up failed.'); }
  const started = performance.now();
  await Promise.all(Array.from({ length: concurrency }, async () => {
    while (next++ < requests) {
      const before = performance.now();
      try { const response = await fetch(target, { headers, signal: AbortSignal.timeout(5000) }); await response.arrayBuffer(); statuses[response.status] = (statuses[response.status] ?? 0) + 1; if (!response.ok) failed++; }
      catch { failed++; statuses.networkError = (statuses.networkError ?? 0) + 1; }
      times.push(performance.now() - before);
      if (pacingMs) await new Promise(resolve => setTimeout(resolve, pacingMs));
    }
  }));
  const durationSeconds = (performance.now() - started) / 1000; times.sort((a, b) => a - b);
  const percentile = p => Math.round(times[Math.ceil(times.length * p) - 1] * 100) / 100;
  return { kind: 'authenticated local read smoke; no production capacity claim', requests: times.length, concurrency, pacingMs,
    durationSeconds: Math.round(durationSeconds * 100) / 100, failures: failed,
    failureRate: failed / times.length, p50Ms: percentile(.5), p95Ms: percentile(.95), p99Ms: percentile(.99),
    requestsPerSecond: Math.round(times.length / durationSeconds * 100) / 100, statuses,
    passed: failed / times.length < .01 && percentile(.95) < p95BudgetMs, p95BudgetMs };
}
if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const result = await measureReadLoad({ baseUrl: process.env.NEXUSHR_LOAD_URL, token: process.env.NEXUSHR_LOAD_TOKEN,
    concurrency: Number(process.env.NEXUSHR_LOAD_CONCURRENCY ?? 5), requests: Number(process.env.NEXUSHR_LOAD_REQUESTS ?? 500) });
  console.log(JSON.stringify(result, null, 2)); if (!result.passed) process.exitCode = 1;
}

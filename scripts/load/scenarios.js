import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend, Rate, Counter } from 'k6/metrics';

// No request/response/token logging, no paid APIs, no CVs, no contact data fixtures.
const base = __ENV.LOAD_BASE_URL || 'http://127.0.0.1:5400';
if (!/^http:\/\/(127\.0\.0\.1|localhost|host\.docker\.internal):5400$/.test(base)) throw new Error('Use the isolated loopback load fixture on port 5400.');
const token = __ENV.LOAD_BEARER;
if (!token || !token.startsWith('Bearer ')) throw new Error('An authorized bearer token is required through the environment.');
const seedCount = Number(__ENV.LOAD_SEED_COUNT || 4000);
if (!Number.isInteger(seedCount) || seedCount < 100 || seedCount > 10000) throw new Error('Seed budget: 100..10000 synthetic candidates.');
const duration = __ENV.LOAD_DURATION || '30s';
if (!/^(15|30|60|120)s$/.test(duration)) throw new Error('Duration budget: 15/30/60/120 seconds.');
const readLatency = new Trend('read_latency', true), writeLatency = new Trend('write_latency', true), errors = new Rate('scenario_errors');
const readOperations = new Counter('read_operations'), writeOperations = new Counter('write_operations');
export const options = {
  scenarios: {
    reads: { executor: 'constant-vus', exec: 'read', vus: 20, duration, gracefulStop: '15s' },
    writes: { executor: 'constant-vus', exec: 'write', vus: 4, duration, gracefulStop: '15s' },
  },
  thresholds: { scenario_errors: ['rate<0.01'], read_latency: ['p(95)<1000'], write_latency: ['p(95)<1000'] },
  systemTags: ['method', 'status', 'scenario', 'name', 'expected_response'], // Never record dynamic resource URLs.
  summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)', 'p(99)'],
  setupTimeout: '5m',
};
const params = name => ({ headers: { Authorization: token, 'Content-Type': 'application/json' }, tags: { name }, timeout: '15s' });
const candidate = email => ({ firstName: 'Synthetic', lastName: 'LoadFixture', email, phoneNumber: '0000000000' });
export function setup() {
  const run = `${Date.now()}-${Math.floor(Math.random() * 100000)}`;
  if (__ENV.LOAD_SKIP_SEED === '1') {
    const response = http.get(`${base}/api/candidates?page=1&pageSize=1`, params('seed-count'));
    if (response.status !== 200 || response.json('totalCount') < seedCount) throw new Error('Existing synthetic fixture is too small.');
    return { run };
  }
  const headers = params('seed');
  for (let offset = 0; offset < seedCount; offset += 20) {
    const batch = Array.from({ length: Math.min(20, seedCount - offset) }, (_, i) => ['POST', `${base}/api/candidates`, JSON.stringify(candidate(`seed-${run}-${offset + i}@example.test`)), headers]);
    const responses = http.batch(batch);
    if (responses.some(r => r.status !== 201)) throw new Error('Synthetic fixture seeding failed; no response body is retained.');
  }
  return { run };
}
export function read() {
  const page = 1 + ((__VU * 17 + __ITER) % Math.ceil(seedCount / 20));
  const response = http.get(`${base}/api/candidate-overview?page=${page}&pageSize=20`, params('candidate-overview'));
  const ok = check(response, { 'overview page is bounded and successful': r => r.status === 200 && r.json('items').length <= 20 && r.json('totalCount') >= seedCount });
  readOperations.add(1); readLatency.add(response.timings.duration); errors.add(!ok); sleep(0.1);
}
export function write(data) {
  const body = candidate(`write-${data.run}-${__VU}-${__ITER}@example.test`);
  const created = http.post(`${base}/api/candidates`, JSON.stringify(body), params('create-candidate'));
  let ok = created.status === 201; writeOperations.add(1); writeLatency.add(created.timings.duration);
  if (ok) { const response = http.put(`${base}/api/candidates/${created.json('id')}`, JSON.stringify({ ...body, firstName: 'Reviewed' }), params('update-candidate')); ok = response.status === 204; writeOperations.add(1); writeLatency.add(response.timings.duration); }
  check(ok, { 'draft create and update succeeded': x => x }); errors.add(!ok); sleep(0.2);
}
export function handleSummary(data) {
  // Metrics only: never include setup data, dynamic IDs, tokens or payloads.
  return { 'summary.json': JSON.stringify({ metrics: data.metrics, scenario: { seedCount, readVus: 20, writeVus: 4, duration }, note: 'Local synthetic fixture; not production capacity evidence.' }, null, 2) };
}

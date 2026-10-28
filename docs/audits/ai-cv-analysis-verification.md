# AI CV Analysis implementation verification

Date: 2026-10-27. Branch: `feature/ai-cv-analysis`, based on main `9fd123d`.

## Delivered

- Pagination overflow regression repair, including int.MaxValue inputs.
- Candidate-owned Domain/Application/Infrastructure/API analysis module; additive PostgreSQL migration and immutable document SHA-256.
- MediatR request/query endpoints, idempotency and one-active-document uniqueness, xmin/If-Match/ETag, immutable human review revisions.
- Transactional request Outbox and short EF Inbox; independent leased worker, cancellation, rate-limit retry cap and recovery. Uncertain external calls retain reservation and require acknowledged explicit retry.
- Atomic daily deployment budget and subject quota; versioned per-attempt usage/pricing and conservative reservation.
- Paged bounded PDF text extractor; compatible contact extraction and Turkish-locale email regression repair.
- OpenAI Responses strict-schema adapter, contact minimization and evidence validation; disabled feature and external-processing defaults.
- Candidate analysis/history/evidence/review UI; CI fake-provider browser integration and operational ADR/runbook.

## Executed locally

| Check | Result |
|---|---|
| .NET 10 Release solution build | Passed, 0 warnings / 0 errors |
| Release solution tests | 111 passed, 0 failed before the two final added regressions; 1 browser-host test explicitly deferred to its dedicated run |
| Final AI PostgreSQL/RabbitMQ/MinIO reliability suite | 20 passed, including added unique-index/xmin and overnight budget regressions |
| Dedicated Release AI browser-host test | 1 passed; real API/PostgreSQL/RabbitMQ/MinIO/PDF, deterministic fake provider |
| Normal Playwright suite | 3 passed: feature-off behaviour, complete existing business workflow, auditor upload denial; isolated fake-host case explicitly deferred |
| Isolated fake-host Playwright case | Passed through the dedicated backend fixture; analysis, evidence, HR review and persistence after reload |
| npm ci / ESLint / frontend production build | Passed; existing >500 kB bundle warning remains |
| npm audit | 0 vulnerabilities |
| NuGet transitive vulnerability report | No reported vulnerabilities |
| Candidate/Hiring/Onboarding migration drift | All passed |
| docker compose config --quiet | Passed |
| Final Candidate API / frontend Docker build and startup | Passed; both healthy; existing infrastructure/data/volumes retained |
| git diff --check; tracked log/tool/result/secret-pattern checks | Passed; local tools/results excluded |

There are 113 distinct non-browser backend test cases after the final added constraint and overnight-budget regressions, plus the dedicated browser-host test. The dedicated browser-host test is counted separately from the normal solution suite, and its underlying Playwright case is the same scenario, not a second independent backend coverage claim.

An initial concurrent container-build/test run timed out in the existing Onboarding messaging test and exposed a recovery test setup race. The latter now waits for Inbox delivery before simulating a crash. Candidate container test classes are serialized to reduce resource contention. Subsequent complete Release suite passed. A simultaneous Windows build/test invocation also hit a locked test assembly; sequential execution passed. These failed attempts were not counted as passes.

The end-to-end AI browser test uses a synthetic Candidate/Hiring chrome fixture, real development Keycloak login, and real AI endpoints/persistence/messaging/storage with fake provider and test-only identity. The separate business E2E uses the actual full application for Candidate → Hiring → Offer Acceptance → Onboarding completion.

## Limits

No paid API request or real-person CV transmission occurred. OpenAI live account/model compatibility, model accuracy/quality evaluation, provider billing reconciliation, retention/deletion procedures, malware quarantine and production deployment remain unverified deployment gates. Strict schema and span validation do not establish factual correctness or hiring suitability. Contact redaction is not guaranteed anonymization. No exactly-once external billing claim is made.

CI source adds a dedicated fake-provider browser job step after application startup. Actual remote CI state is reported in the PR; local results above are not represented as remote CI evidence.

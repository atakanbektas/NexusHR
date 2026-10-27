# Integration verification — 2026-10-27

Branch: `codex/platform-integration`. Baseline preserves the existing Hiring, offer-response and Employee Onboarding development plus the PR #4 upload authorization fix. The four previously pending changes were completed; the pre-existing Onboarding launch profile was retained. No data volumes were reset and no main merge was performed.

## Milestones and changed files

1. Persistence/reliability: Candidate upload coordinator and durable cleanup intents, Candidate/Hiring xmin mappings and migrations, Onboarding initial migration/factory/solution inclusion, bounded PDF validation, snapshot locking, PostgreSQL/MinIO regression tests, completion event/consumer and real RabbitMQ outage/Inbox tests. Main files are under Candidate Application/Domain/Infrastructure/tests, Hiring Infrastructure, Onboarding Application/Infrastructure/tests and Contracts.
2. Frontend: CandidateEditor, same-draft CV recovery, Onboarding MUI compatibility, role navigation preservation, Playwright configuration/full-stack browser tests and patched dependency lockfile.
3. Operations/presentation: shared Hosting/Security defaults, development-only fixture configuration, API Dockerfiles/health checks, source-built MinIO, Compose readiness, CI/publishing/dependency updates, README/architecture/ADRs/deployment guidance.

The exact file manifest is in `integration-changed-files.txt`; full patches belong to the integration PR.

## Executed results

| Check | Result |
| --- | --- |
| .NET SDK / Node / Docker Engine access | .NET 10.0.403, Node 24.22.1; Linux Docker Desktop engine reachable outside sandbox |
| `dotnet build NexusHR.slnx --no-restore --configuration Release -m:1` | PASS, 0 warnings, 0 errors |
| `dotnet test NexusHR.slnx --no-build --configuration Release -m:1` | PASS: 72 passed, 0 failed, 0 skipped |
| Candidate integration | 16 passed: HTTP role matrix plus real PostgreSQL/MinIO upload success, uniqueness/concurrency and failed-compensation recovery |
| Candidate unit | 11 passed: domain transitions, verified snapshot lock and null/spoofed/oversized CV validation |
| Hiring unit | 31 passed: domain/offer/token/query behavior |
| Onboarding | 14 passed: domain, real HTTP role/validation/error behavior, PostgreSQL migration/concurrency, real RabbitMQ Inbox deduplication and Outbox recovery after broker outage |
| `npm ci` | PASS using workspace-local npm CLI; clean native install after dependency patching |
| Frontend lint and TypeScript/Vite production build | PASS; Vite reports a non-blocking bundle-size advisory |
| `docker compose config --quiet` and full build/start | PASS; business APIs, databases, storage, identity, broker and web readiness checked |
| Playwright full-stack tests after dependency patching | PASS: 2/2, no retries; real Keycloak, gateway, PDF, MinIO, PostgreSQL, broker, MailHog and browser |
| NuGet vulnerability report, including transitives | No vulnerable packages reported across 23 projects |
| npm dependency audit after patching | PASS: 0 info/low/moderate/high/critical findings; final JSON retained locally |
| Model/migration drift (Candidate, Hiring, Onboarding) | PASS: no pending model changes in all three contexts |

Browser scenario: sign in as HR, extract a real indexed PDF, register/upload/download, verify, wait for eligibility, create hiring, prepare/send offer, receive email, accept anonymously, track/complete tasks, complete onboarding and observe Hiring Completed. A separate auditor session verifies read access and a forbidden CV upload.

The RabbitMQ test first verifies duplicate transport/business deliveries create one onboarding. It then stops the broker application while keeping Docker's host port stable, commits completion plus Outbox while unavailable, restarts the broker and verifies the pending Outbox is delivered. No failure was hidden or test skipped.

## Evidence and limits

Local raw evidence is retained under ignored `.tools/`: `release-build.log`, `release-tests.log`, TRX files, `e2e-patched.log`, clean npm/build/lint logs, Compose logs and audit JSON. Playwright HTML/trace artifacts are ignored. CI uploads its own reports; a configured workflow is not itself evidence of a green GitHub run. Remote CI/publishing/deployment results are separate from these local results.

Docker pipe and network access require sandbox escalation in this environment. Both were exercised successfully, rather than assumed from the user's hello-world test.

Production remains deployment-specific: MinIO upstream is archived (ADR 003), SMTP can duplicate mail, OCR/antivirus/HA/payroll are absent, and managed secrets/TLS, audience mapping, protected environments, backup/restore and load testing are not certified by these checks. Existing orphan objects from older versions are not automatically removed. Screenshot placeholders are explicitly marked. No successful production deployment is claimed.

# Integration roadmap and acceptance

This integration preserves the Hiring/offer-response/onboarding branches and the PR #4 upload authorization fix. Historical source-only findings are retained in the initial audit. Current execution evidence is in [integration-verification.md](audits/integration-verification.md).

| Milestone | Implementation | Acceptance |
| --- | --- | --- |
| Persistence and concurrency | Onboarding migration/test inclusion; Candidate/Hiring xmin mappings and migrations | Real PostgreSQL schema, uniqueness and stale-write tests; migration/model drift check |
| CV consistency | Durable reservation, bounded object upload, atomic metadata/status transaction, outcome-aware compensation and reconciler | Real PostgreSQL/MinIO success, concurrent-upload and failed-compensation recovery tests |
| Business integration | Candidate eligibility, offer email/response, accepted-offer onboarding, task transitions and final Hiring completion | Real browser/Keycloak/RabbitMQ/SMTP/storage end-to-end test |
| Functional frontend | Existing Candidate/Hiring preserved; editable pre-verification contact data, upload recovery, Onboarding screens | TypeScript production build, ESLint and browser workflow/security tests |
| Delivery and operations | Container health/readiness, non-root .NET runtime, external production configuration, CI and explicit GHCR publishing | Compose health checks, dependency audits and CI run evidence |
| Portfolio | README, architecture, ADRs, setup/recovery and verification report | Fresh-stack demo and accurate feature/limitation descriptions |

Portfolio hardening adds immutable CV versions, per-fact review, local OCR, fail-closed scanning, Candidate DB/S3 restore rehearsal, lazy frontend routes and a bounded read-load probe. See [execution evidence](audits/portfolio-hardening.md) and [operations](operations.md). Production acceptance still requires maintained storage, secrets/TLS/proxy setup, protected release environments, whole-platform recovery, HA/capacity, retention and organizational data-access rules. No production deployment or exactly-once SMTP guarantee is claimed.

# Engineering hardening: application boundary review

Date: 2026-11-02. Baseline `045630a` on main, clean worktree. Scope includes Candidate, Hiring, Onboarding, Notification, Gateway, shared hosting/security, CI and Docker. Open-PR search returned the old upload-authorization PR #4; no dependency-upgrade branch was blindly merged. Follow-up operational evidence is separate.

## Verified strengths

Domain boundaries, distinct CV/editor/reader roles, no Admin override, aggregate xmin, DB uniqueness, durable object upload intents/compensation, scoped Outbox/Inbox and public expiring single-use offer capabilities already exist. Existing PostgreSQL/RabbitMQ failure tests and full business E2E remain authoritative; no architecture rewrite or new microservice is justified. SMTP can duplicate after acknowledgement loss; no exactly-once claim is made.

## Findings and actions

| Priority | Evidence | Action / residual risk |
| --- | --- | --- |
| High | Gateway `CandidateOverviewService.GetAsync/GetAllCandidatesAsync` downloaded every candidate and requested every Hiring batch before paginating | Normal listing now delegates pagination/count and enriches only that page. Cross-service display filters preserve semantics with a 5000-record budget and 20s total deadline; large filtered searches require narrowing, not silent truncation |
| Medium | Same service computed `(page-1)*pageSize` as int | Long offset and bounded Skip. Extreme-page regression proves empty result instead of returning the first page |
| Medium | Candidate repository ordered only by CreatedAtUtc; no supporting sort index | Deterministic CreatedAtUtc/Id order and additive descending index. Actual PostgreSQL same-timestamp page test |
| Medium | Claims transformation parsed malformed JSON/non-string role entries without guarding; mixed arrays could be partially applied | Bounded parse and atomic role-shape validation. Valid role mapping remains idempotent; no issuer/signature requirement changes |
| Medium | Downstream errors retained raw bodies in exception text; shared unhandled exception logs included arbitrary exception payloads | Discard downstream body and log only safe exception type/correlation. Minimal API errors use compatible code/errors plus ProblemDetails; synthetic privacy regression checks HTTP and captured logs |
| High for department-restricted deployments | Current roles are organization-wide and Candidate has no pre-Hiring department ownership | ADR 007 explicitly records the intended single-employer boundary and limitations. No fabricated tenant column/free-text security filter. Organization acceptance or a separate authoritative assignment/claims/backfill change is required |
| Medium, remains | Legacy Onboarding collection reads all tasks/records; Notification has retry but no durable SMTP delivery ledger; document-format attacks can still exhaust native/PDF parsing limits | Document these operational/product limits; avoid claiming complete scale/isolation or exactly-once mail |

Resource-level HTTP tests verify candidate/document/analysis ID substitution returns 404 without altering the original analysis. Existing CV permissions remain unchanged. Existing public-offer rate/body/token/concurrency checks are preserved. Clean Domain dependency guards were added.

## Verification

- Baseline: .NET 10.0.403 / Node 24.22.1. Build zero warnings/errors; 173 ordinary backend tests passed, 15 explicit native/browser opt-ins skipped. Docker was initially stopped; Desktop was started before infrastructure tests completed. No business data/volume was reset.
- Eight of eleven new Gateway/identity cases failed against the original implementation and passed after restoring the fixes. This is an executed red/green reproduction, not assumed coverage.
- Application milestone: build zero warnings/errors; ordinary suite 188 passed (23 Candidate unit + 107 Candidate infrastructure/HTTP + 31 Hiring + 14 Onboarding + 13 Platform); 15 opt-ins remain explicitly separate.
- Latest targeted resource/concurrency/analysis suite: 32 passed; Platform architecture/HTTP/identity/privacy suite: 13 passed. Poison-schema handling was added to the real existing broker outage/dedup test: bounded retries, durable `_error` queue, no partial row and subsequent valid message processing.
- Initial new-test compilation failures (missing namespace imports) were corrected. One overlapping log-file write was rejected on Windows; suites were then serialized with distinct outputs. No failing assertion was disabled or counted as passing.
- Browser, migration drift, dependency and final GitHub CI results are recorded in the PR and operational follow-up; do not treat a running check as a pass.

Migration creates an index without deleting business rows. On large deployments apply it during a controlled maintenance/migration window and measure locks before release. No zero-downtime or cross-database snapshot consistency is claimed. No main merge or production deployment is performed.

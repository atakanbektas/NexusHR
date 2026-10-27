# AI CV Analysis

AI analysis is optional and **off by default**. Registration, deterministic contact extraction, document verification, Hiring and Onboarding do not wait for AI. No real CV or paid provider call is required by CI.

## Business behaviour

The Candidate detail page has General information and CV Analysis tabs for CV readers. HR can request analysis, follow status/history, inspect source evidence, append an approve/reject/correct review and cancel/retry within policy. A correction is an audited human annotation, not an overwrite of machine evidence or verified Candidate contacts.

Facts cover skills, employment, education, declared languages and certifications. Unsupported evidence is rejected. No ranking, suitability score, protected-trait inference or automatic hiring decision is implemented. Grounding checks verify text spans, not the truth of statements made in a CV or the semantic correctness of every model categorization; human review is required.

CV readers remain HrSpecialist, HrManager and Auditor. Only HrSpecialist writes. DepartmentManager, IT and Admin receive no implicit CV/analysis access. Authentication and authorization are enforced at the API, independently of UI visibility. Retry is restricted to the original requester to keep user quota attribution accurate.

## API

Base: `/api/candidates/{candidateId}/cv-analyses`.

| Method | Path | Contract |
|---|---|---|
| GET | base + `?page=1` | 20-item paginated history, current DocumentId and effective settings; works while feature is off |
| POST | base | `{documentId, acknowledgeExternalProcessing}`; `Idempotency-Key` header, max 100 characters; 202 + Location |
| GET | `/{analysisId}` | State, version, validated facts/evidence, review revisions and attempt usage |
| POST | `/{analysisId}/cancel` | `If-Match: "version"`; queued cancellation or running best effort |
| POST | `/{analysisId}/retry` | If-Match; `{acknowledgePossibleDuplicateCharge}` for OutcomeUnknown |
| PUT | `/{analysisId}/review` | If-Match; `{decision: "approve"|"reject"|"correct", notes}`; correction requires notes, max 4000 chars |

Single-resource responses expose ETag and body version. Missing If-Match gives 428, stale versions 409. Other errors use ProblemDetails with stable `code`: 400 validation/acknowledgement, 403 subject/retry ownership, 404 resource, 409 state/idempotency/active document, 429 budget/quota and 503 disabled. Replaying the same key/document/user returns the original run without another reservation; a different document for that key conflicts. Keys are scoped to candidate and authenticated subject. Analysis IDs cannot bypass candidate scoping.

States: Queued, Running, Succeeded, Failed, Refused, OutcomeUnknown, Cancelled. Error codes and attempt records explain terminal failures. No generated result is exposed for failed/cancelled runs.

## Configuration and cost controls

The backend binds `CvAnalysis` configuration. Keep API keys exclusively in server environment/secret-manager configuration; never commit them, place them in VITE variables, or paste them into scripts/logs. Docker Compose deliberately provides no paid-AI enablement or secret defaults.

| Environment key | Default / requirement |
|---|---|
| `CvAnalysis__Enabled` | false |
| `CvAnalysis__AllowExternalProcessing` | false; separate operational privacy gate |
| `CvAnalysis__ApiKey` | no default; backend secret only |
| `CvAnalysis__Model` | explicit structured-output-capable model snapshot required when enabled |
| `CvAnalysis__PriceVersion` | explicit reviewed tariff identifier required |
| `CvAnalysis__InputUsdPerMillion`, `CvAnalysis__OutputUsdPerMillion` | explicit positive reviewed USD rates; no hard-coded current tariff |
| `CvAnalysis__DailyBudgetUsd` | 5 USD |
| `CvAnalysis__RequestsPerUserPerDay` | 20 reservations; cancellations/retries still consume request quota |
| `CvAnalysis__MaxInputCharacters` | 12000 UTF-16 code units; rejected rather than silently truncated |
| `CvAnalysis__MaxOutputTokens` | 2000 |
| `CvAnalysis__TimeoutSeconds` | 60 |
| `CvAnalysis__MaxAttempts` | 2 total, including explicit retry; configurable 1–3 |

Conservative reservation:

`((6 × MaxInputCharacters + 8192) × inputRate + MaxOutputTokens × outputRate) / 1,000,000`, rounded upward to 8 decimals.

Six bytes per code unit covers JSON escaping; 8192 covers prompt/schema/page structure. This is deliberately more conservative than word/token heuristics. Actual usage settles with versioned per-attempt rates; cached-token discounts are not assumed. Spend is an application estimate, not a provider invoice. Unknown usage retains its entire reservation. Positive overage is recorded rather than hidden and blocks further reservations when the daily cap is reached. Deployment-wide budget and per-subject quotas are atomic across replicas. Parallelism is two per worker instance, not a global replica limit.

Model/reservation increases after a restart fail old queued jobs before dispatch until explicitly retried with a fresh reservation. No expensive-model fallback is configured. Provider-side project budgets are recommended as a separate control, never as the sole application limit.

## Privacy and provider contract

The OpenAI adapter uses HTTPS Responses `/v1/responses`, `store:false`, strict JSON Schema and a maximum output budget. It sends extracted text only; no PDF/file upload, MinIO URL, tool execution or web access. Known contact fields and email/phone patterns are blanked while preserving offsets. This is best-effort minimization, not anonymization: CV text can still contain personal information.

CV text is untrusted data; its instructions are not executed. Output renders as escaped React text. No CV/prompt/result is intentionally logged; worker diagnostics include exception class only, not raw provider bodies. Default EF sensitive-data logging stays off.

`store:false` does not mean zero retention. Before real-person processing, an operator must settle lawful purpose/basis, provider agreement, region, retention/deletion, data-subject handling, malware/quarantine, access review and human decision policy. These are not certified by this implementation. No real-person pilot or production deployment was performed.

Official contracts reviewed:
- [Structured Outputs](https://developers.openai.com/api/docs/guides/structured-outputs)
- [Data controls](https://developers.openai.com/api/docs/guides/your-data)

## Persistence and recovery

The additive Candidate migration creates `cv_analyses`, `cv_analysis_attempts`, `cv_analysis_reviews`, `ai_daily_budgets`, `ai_daily_quotas` and nullable document ContentHash. Existing data/volumes are preserved. Existing deployment migration policy remains: auto migration in Development, controlled migration in production.

Run + request Outbox + reservation commit together. Inbox delivery does not hold locks during provider HTTP. A short global AI advisory transaction lock protects state/budget changes; xmin and unique indexes also guard stale/duplicate operations. SHA-256 binds evidence to immutable CV content. Document replacement remains outside this release.

Recovery procedure:
1. Inspect run state, attempt times, dispatch marker, provider request ID (if known), error code and reservation; do not copy CV text into tickets/logs.
2. Inspect worker liveness and RabbitMQ `candidate-cv-analysis_error` queue. Durable queued work also has a DB fallback after 30 seconds.
3. Expired Running jobs are reconciled automatically. Pre-dispatch failures release budget; dispatched/uncertain outcomes retain it. Resume healthy worker/DB infrastructure before replaying messages.
4. Retry known failures within cap. For OutcomeUnknown, the original HR requester must explicitly acknowledge possible duplicate charges. Cancellation after dispatch does not promise a refund.
5. Reconcile uncertain spend against provider records/invoices operationally. There is no automatic provider-invoice reconciliation, deletion API or privileged budget-reset endpoint; do not edit budget rows to bypass caps. Old-day unknown reservations do not reduce a later day's independent cap. Define approved retention/finance procedures before production.

## Reproducible checks

Prerequisites: .NET 10, Node 24/npm, Docker, pinned `nexushr-minio:2025-10-15` image built with `docker compose build candidate-storage`.

```sh
dotnet build NexusHR.slnx
dotnet test NexusHR.slnx --logger trx
dotnet tool restore
dotnet ef migrations has-pending-model-changes --project NexusHR.Candidate.Infrastructure --startup-project NexusHR.Candidate.Api
cd NexusHR.Web
npm ci
npm run lint
npm run build
npm audit --audit-level=high
cd ..
docker compose config --quiet
docker compose up -d --build --wait
cd NexusHR.Web
npx playwright install chromium
npm run test:e2e
```

Normal backend tests explicitly skip the browser-host test until the application and browsers exist; normal Playwright explicitly skips the isolated fake-host case because its fixture owns those credentials/ports. CI then executes that case through the real backend fixture with:

```sh
RUN_AI_BROWSER_TESTS=1 dotnet test NexusHR.Candidate.IntegrationTests --filter FullyQualifiedName~AiBrowserTests
```

PowerShell: `$env:RUN_AI_BROWSER_TESTS='1'` before the dotnet command. Set PLAYWRIGHT_BROWSERS_PATH only if using a nondefault browser cache.

That test starts isolated PostgreSQL/RabbitMQ/MinIO and the real Candidate API, worker, PDF parser, Outbox/Inbox and deterministic provider. Playwright relays AI requests to that real test host; surrounding Candidate/Hiring chrome uses synthetic fixture responses, while browser login uses development Keycloak. Test authentication/provider live exclusively in the test assembly. A separate full-stack E2E covers the unchanged real Candidate → Hiring → Onboarding path with AI off.

No paid model-quality/latency benchmark has been performed. Contract tests and a deterministic fake verify protocol/reliability, not production LLM accuracy. Model snapshot selection and a representative synthetic TR/EN evaluation corpus remain required before external processing is enabled.

# ADR 004 — Optional Candidate-owned CV analysis

Status: implemented, feature disabled by default.

Candidate owns CV documents, their access policy and derived analysis. A new service or vector database would add coordination without a current business need. Domain contains run state transitions; Application contains MediatR requests, provider/text/store abstractions and evidence validation; Infrastructure contains EF, PDF, MassTransit and the OpenAI HTTP adapter. API owns role and conditional-request enforcement.

An analysis request reserves a versioned worst-case USD budget and user quota and saves the run and scoped bus Outbox in one PostgreSQL transaction. A short EF Inbox consumer marks delivery. A separate bounded worker claims durable jobs under a lease and optimistic version. No provider call runs inside a database transaction or the Inbox consumer transaction. PostgreSQL advisory transaction locks serialize short AI budget/state writes across instances; unique indexes independently enforce request identity and one active run per document.

The worker polls every two seconds, with two concurrent executions per instance. Jobs older than 30 seconds can be recovered directly from the durable queue even if broker delivery is unavailable. Expired pre-dispatch leases become Failed and release the reservation; dispatched leases become OutcomeUnknown and retain it. Recovery inspects at most 100 expired jobs per cycle.

External provider calls cannot be atomic with PostgreSQL. A dispatch marker is committed before sending; crashes in the marker-to-send gap are conservatively treated as unknown. Unknown outcomes are never automatically repeated. An original requester may explicitly acknowledge possible duplicate charges and retry, subject to a total attempt cap. Cancellation stops queued work, checks before dispatch and discards returned results if cancellation arrived during the call; it cannot promise a refund.

Only request-rate 429 responses receive bounded delayed retry. Retry-After is respected; values over five minutes remain visible failures for manual handling. Billing/quota errors, refusals, malformed responses and uncertain 5xx/timeouts are not blindly retried. There is no hidden SDK retry layer.

Results are professional facts with exact page/UTF-16 offset/quote evidence. Values must occur verbatim within their evidence. This intentionally narrows the original free-form summary proposal: the UI presents a factual overview instead of generating an unsupported narrative. Human approvals, rejections and correction notes are immutable review revisions; original evidence remains unchanged. Analysis never updates Candidate verification, Hiring eligibility or employment decisions.

Existing deterministic contact extraction reuses the paged text extractor. Routes and response shapes remain compatible; culture-invariant email matching fixes Turkish-locale truncation. Existing CVs acquire a nullable content hash lazily when analyzed; new uploads record it immediately. The additive migration does not reset data or implement CV replacement/versioning.

Feature enablement and permission to process externally are separate switches, and each external run requires an acknowledgement. The only production adapter is OpenAI; fake provider and authentication exist only in the integration test assembly. Provider options have no model/pricing/key defaults that could silently initiate paid work.

Consequences: one extra durable job module in Candidate; conservative unknown charges can consume a whole daily budget; global budget writes serialize briefly; paid quality evaluation, data retention decisions and operational reconciliation remain deployment gates. See [runbook](../ai-cv-analysis.md).

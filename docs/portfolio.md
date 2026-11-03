# Portfolio presentation

NexusHR is a local-first, single-employer HR workflow portfolio application. Present verified engineering decisions and trade-offs; do not imply production customer use, multi-tenancy, exactly-once SMTP or certified AI accuracy.

## Suggested CV entry

**NexusHR — .NET 10 / React HR onboarding platform**

- Built Candidate → Hiring → offer acceptance → employee onboarding workflows using Clean Architecture, CQRS/MediatR, EF Core/PostgreSQL and RabbitMQ/MassTransit transactional Outbox/Inbox.
- Implemented role-specific CV authorization, optimistic concurrency, versioned documents and durable PostgreSQL/MinIO compensation and reconciliation.
- Integrated optional local Ollama/Tesseract CV analysis with deterministic source-evidence validation, cancellation, idempotency, budget controls and separate human review.
- Added Docker orchestration, GitHub Actions, PostgreSQL/MinIO integration tests, Playwright business/security checks and an isolated engineering observability/load/recovery lab.

Turkish summary: “.NET 10 ve React ile aday kaydından işe alım ve çalışan onboarding sürecine uzanan; Outbox/Inbox, optimistic concurrency, kaynakla doğrulanan yerel AI analizi ve gerçek altyapı testleri içeren HR platformu geliştirdim.”

## Five-minute demonstration

1. Start the documented development stack and use the development HR account from the README. Use only a synthetic CV.
2. Register a candidate, upload the CV and show extraction and the immutable document record. If the optional AI stack is enabled, start analysis and inspect its evidence and separate human-review decision.
3. Verify the document, wait for Hiring eligibility, prepare/send an offer and open the capability link from local MailHog. Accept it.
4. Show the automatically created onboarding process, complete its tasks as the appropriate roles and finish onboarding as HR. Confirm eventual Hiring completion.
5. Sign out and use a separately provisioned engineering account. Show live service telemetry and a retained synthetic load/recovery report. Explain why this account cannot read CVs and why experiments run in a separate disposable lab.

## Useful interview topics

Explain why PostgreSQL and MinIO need compensation rather than an alleged shared transaction; how `xmin`, database uniqueness and idempotency keys solve different races; how Outbox/Inbox handles message redelivery; why a quote must match source text independently of model confidence; and why a laptop benchmark does not prove production capacity.

Use [architecture](architecture/overview.md), [ADRs](adr), [engineering verification](audits/engineering-control-center.md), [operations](operations.md) and the README screenshots as supporting material. Hosted production acceptance remains separate: maintained storage, organizational retention/access policy, secrets/TLS, whole-platform restore, capacity and release approvals.

# Architecture overview

Current integration: Candidate, Hiring, Onboarding and Notification with YARP, Keycloak, PostgreSQL, RabbitMQ and MinIO. The Candidate-only initial audit is historical; current execution evidence belongs in the integration verification report.

## Boundaries

Domain owns business invariants without infrastructure dependencies. Application owns MediatR commands/queries and interfaces. Infrastructure implements EF, storage, broker and SMTP adapters. APIs own HTTP contracts/roles. Onboarding uses pipeline validation; existing Candidate/Hiring handlers retain explicit validators and compatible response contracts.

Candidate owns contacts, CV metadata and verification. Hiring stores an eligible-candidate event projection, offers and decisions. Onboarding owns the accepted employee profile and default IT tasks. Its aggregate ID is the employee reference; there is no separate employee microservice.

## Event flow

Candidate verification commits ReadyForHiring plus Outbox → Hiring Inbox creates eligibility → hiring/offer → OfferSent Outbox → Notification/SMTP → anonymous acceptance commits OfferAccepted plus Outbox → Onboarding Inbox creates a unique process and three tasks → HR completion commits OnboardingCompleted plus Outbox → Hiring Inbox marks Completed.

Business services have independent databases. Scoped EF bus Outbox commits messages with state changes; consumer EF Inbox/Outbox and unique business keys tolerate duplicates. Retry intervals are 1/5/15 seconds, with exhausted messages in MassTransit `_error` queues. Inbox retention is finite; SMTP delivery is not exactly-once.

## Persistence

`xmin` optimistic concurrency protects Candidate, Hiring and Onboarding aggregates. Unique indexes protect normalized email, one CV per candidate, one active hiring process and one onboarding per hiring process. Conflicts map to ProblemDetails HTTP 409. Candidate contact updates stop after verification to preserve the Hiring snapshot.

Candidate create/update repositories commit individual commands; verification publishes to scoped Outbox before its repository save. CV upload uses a dedicated coordinator: save durable intent, upload under a two-minute deadline, lock the intent, then commit document metadata/status/intent deletion together. Compensation checks the persistent outcome in a fresh context before deleting an object. A reconciler handles expired intents every minute, in batches of 100, after a 15-minute reservation window. It takes the same lock, checks document references and cannot delete a finalized object. PostgreSQL/MinIO atomicity is not claimed. See ADR 001.

## Operations and security

Keycloak OIDC/PKCE authenticates the browser. Gateway and APIs independently enforce authentication; APIs enforce realm roles. Offer responses are anonymous capability-token operations with hash/expiry/single-use checks, rate/body limits and concurrency protection. Production requires external configuration, HTTPS identity metadata and an audience mapper.

Shared Hosting provides JSON logs, ProblemDetails with compatible code/errors fields, correlation IDs, health checks and optional OTLP export. Compose retains named volumes; migrations run automatically in Development only unless explicitly enabled. Production schema changes need a controlled rollout.

Limits: SMTP may duplicate mail, PDF parsing is not antivirus/OCR, MinIO upstream is archived, and single-machine Compose is not an HA/production deployment. See README, deployment notes and current verification report.

## Optional CV analysis

Candidate also owns optional asynchronous CV analysis; it never gates verification or Hiring. Request state and budget reservation commit with the bus Outbox, a short EF Inbox marks delivery, and a separate leased worker invokes the provider outside DB transactions. Unknown external outcomes retain budget and require explicit retry acknowledgement. Read [ADR 004](../adr/004-async-cv-analysis.md) and the [runbook](../ai-cv-analysis.md). The feature and external processing are disabled by default.

An opt-in Docker Ollama adapter now shares the same Candidate job infrastructure. Provider-specific accounting retains paid OpenAI reservations while local runs keep quotas with zero external API cost. Local model readiness gates submission; literal quotes are resolved into authoritative spans and validated. Inference has an internal-only network, separate from model download. See [ADR 005](../adr/005-local-ollama-provider.md) and [local setup](../local-ai-ollama.md).

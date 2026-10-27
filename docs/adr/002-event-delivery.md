# ADR 002: EF Outbox/Inbox and business idempotency

Status: implemented; broker verification is reported separately.

Use scoped MassTransit EF bus Outbox to save events with business writes. Consumer endpoints use PostgreSQL Inbox/Outbox, bounded retries and durable RabbitMQ queues. Unique HiringProcessId prevents duplicate onboarding creation. Matching completed Hiring records accept duplicate completion events safely. Schema versions are explicit.

Consistency across services is eventual. UI refresh and bounded test polling accommodate propagation. Exhausted deliveries remain in `_error` queues for inspection/replay after the cause is repaired. Inbox retention is finite; business keys add protection beyond transport deduplication.

SMTP cannot share an atomic database transaction: acknowledgement ambiguity can produce duplicate emails. One-use offer tokens prevent duplicate decisions; exactly-once email delivery is not claimed.

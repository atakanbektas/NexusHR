# ADR 009 — Metadata control plane with a separate development execution boundary

Accepted 2026-11-03. See [the control-center guide](../engineering-control-center.md).

## Existing capabilities and gaps

The repository already had shared OpenTelemetry OTLP hooks, structured JSON logs, correlation/health endpoints, EF Outbox/Inbox, real persistence/recovery/security tests, Playwright, a bounded isolated k6 workload and CI/image traceability. It had no telemetry storage/query stack, engineering permission, dashboard, durable experiment scheduler or privileged failure-test boundary. Candidate, Hiring, Onboarding and Notification retain their existing ownership and databases.

## Decision and implementation sequence

1. Put the protected Engineering API in Gateway, not a new business microservice. Explicit EngineeringReader/EngineeringOperator roles do not imply candidate/CV access; Admin/HR roles do not imply engineering access.
2. Keep engineering history/audit/leases in a separate PostgreSQL database. Queue admission and claims serialize across replicas; idempotency and lease fencing are enforced in PostgreSQL. Cancellation is durable; a stale experiment is recovered, never blindly replayed.
3. Use Collector → Prometheus/Tempo/Loki, plus Grafana and container/Linux-VM exporters. Redact at the collector before storage. Keep backend query templates fixed, responses bounded and partial source failures explicit. No user SQL, PromQL, LogQL, arbitrary target URL, raw broker payload or GitHub write operation.
4. Use an explicitly started local Node agent as the privileged execution boundary. Gateway has no Docker socket or process execution. The agent accepts only bounded jobs for a fixed disposable Compose project, checks its mounts/labels, and uses an independent cleanup watchdog. Execution fails closed outside Development.
5. Exercise real sources, low-load k6, cancellation, faults/recovery, authorization and telemetry privacy; rerun the existing business regression suite. Expensive performance checks stay opt-in, not routine CI.

## Trade-offs

Metadata polling and redaction deliberately reduce diagnostic detail. Query fingerprints are not SQL text; no arbitrary EXPLAIN endpoint is provided. Span timing/parentage does not prove a causal critical path. Partial source outages are not rendered as zero. Docker VM metrics are not Windows host measurements. Shared-host synthetic tests are observations rather than certified production capacity.

cAdvisor and the local agent are privileged host boundaries; a read-only socket mount is not a read-only Docker API. Neither is safe to expose as a public multi-user execution service. Private backend listeners, generated credentials, strict target validation and development-only admission reduce exposure, but do not defend against an attacker who already controls Docker/the host. Existing business data/volumes are never experiment targets.

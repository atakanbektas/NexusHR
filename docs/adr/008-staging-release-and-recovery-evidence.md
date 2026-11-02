# ADR 008: Protected release identity and isolated recovery acceptance

Status: Implemented, verification limits explicit, 2026-11-02.

Retain existing services and infrastructure. No new production microservice, paid provider, tenant model or backup daemon is introduced. Application hardening and operational verification are separately reviewable PRs.

Staging is a strict configuration environment: HTTPS identity/discovery/audience, explicit HTTPS CORS origins, certificate-validating RabbitMQ TLS and enabled Candidate malware scanning. PostgreSQL readiness includes migration history, not only connectivity. TLS 1.2/1.3 is configurable in every bus adapter; local Development stays compatible. No callback accepts invalid certificates. External CA/network deployment acceptance is distinct from configuration/unit/local-broker verification.

Publishing remains manual from main with protected environment settings. Require successful main-push CI for the exact revision, include OCI revision/source labels and retain the immutable image-digest manifest. Staging application images/secrets are independent from Development. Startup migrations stay off; rollback uses the prior compatible manifest or reviewed forward migration rather than destructive down scripts.

Use actual disposable PostgreSQL/RabbitMQ/Keycloak/MinIO infrastructure for recovery rehearsals. Quiesce fixture writers, restore to new targets, verify cross-service IDs/task state/CV hash, preserve broker node identity and persistent messages, and verify identity users/roles/audience/signing key after database restore without reimport. These component drills supplement existing Outbox/Inbox outage/replay and retained-CV history tests. They do not certify coordinated live cutover, cluster HA, encrypted offsite custody or RPO/RTO.

Keep k6 workloads on independent synthetic stores with bounded read/write scenarios and explicit failed thresholds. No load request targets business databases or AI. Report host, workload, execution order, percentiles/error/counts and confounding limits; local results are not production capacity.

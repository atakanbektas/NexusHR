# Operations acceptance and recovery

The repository's local verification is not production certification. Keep reviewed release commits, dependency/image inventories, operator access and deployment-specific acceptance evidence together. No script should purge queues, reset databases/volumes, or overwrite a live environment to repair failures.

Current executable follow-up: [staging/release procedure](staging.md), [k6 read/write fixture](load-testing.md), [component recovery and verification results](audits/recovery-performance-readiness.md), [ADR 008](adr/008-staging-release-and-recovery-evidence.md).

## Proposed recovery objectives and cutover acceptance

Proposed starting objectives for a small single-employer installation: at most 24 hours of acknowledged data loss (RPO) and restoration of the complete service within four hours (RTO). These require owner approval, encrypted daily backups, monitored failures, tested key custody and a measured staging cutover. They are **objectives, not verified guarantees**; a one-minute synthetic fixture test is not the platform's RTO.

Quiesce all application writers, consumers/outbox dispatchers and email delivery before taking the coordinated snapshot. Preserve all three business DBs, Keycloak DB/signing material, CV versions, matching RabbitMQ node data/definitions/cookie, application revision and configuration references. Database/object dumps are not a distributed atomic transaction. Hold restored outbound email and external AI off until checking references, hashes, Inbox/Outbox state, error queues and capability expiry; do not purge/replay everything blindly. SMTP duplicates remain possible.

The automated drills now restore three business DBs into a different PostgreSQL target and copy the referenced CV to a different bucket, restore actual broker topology/persistent messages onto a new same-named node, and restore a synthetic Keycloak database onto a new server without realm reimport (user login, role, audience and signing key checked). Existing separate CV rehearsal restores retained versions, reviews and byte hashes. New fixture credentials are generated at runtime; plaintext fixture archives are never production backup artifacts.

Before real cutover, preserve the Keycloak public hostname/issuer and validate old/new session/token expiry behavior; verify every release digest/configuration/secret version. Native broker restoration requires the original node name; a new node name is not a supported substitute for copied queue data. Test the complete synthetic workflow after reconstruction before enabling real traffic. Cluster recovery, encrypted offsite restoration, certificate rotation and timed whole-system cutover still require the deployment environment. See [RabbitMQ backup/restore constraints](https://www.rabbitmq.com/docs/backup).

## Release and security boundary

The AI foundation/local-provider/evidence/portfolio layers were integrated through PRs #23–#26. Current application/operations hardening is reviewed separately starting with PR #36. Review each layer, validate the combined release candidate, and tag only the reviewed integration. Main is never pushed directly or automatically merged by this hardening work. Container publishing remains an explicit protected-environment action; it does not deploy infrastructure.

Development defaults contain public fixtures. Production requires external secrets, HTTPS identity/audience configuration, TLS ingress, restricted management/database ports and a maintained S3-compatible service. Archived local MinIO is retained for reproducible testing, not recommended as a new production storage deployment. Test S3 compatibility and object hashes before migrating existing data; never delete the source until recovery acceptance and the organization's retention policy allow it.

Optional document scanning uses pinned ClamAV 1.5.4. `docker-compose.security.yml` updates signatures in a short-lived downloader, then runs the scanner on an internal-only network without published ports. Upload validation, text extraction and stored downloads fail closed when an enabled scanner rejects data or is unavailable. Candidate Production startup refuses disabled scanning. This is defense in depth, not a guarantee that all malicious files are detected. Re-run the signature updater and roll the scanner on an approved schedule; monitor updater failure and engine readiness. Do not forward CVs to public scanning services.

## Backup consistency and restore drill

1. Schedule a maintenance window and stop application writers/workers. A DB dump and S3 copy are not an atomic distributed snapshot. Include Candidate, Hiring, Onboarding and Keycloak databases, immutable CV objects, and RabbitMQ durable storage/definitions using the broker's supported stopped-node backup procedure. Keep software versions/configuration with the manifest; secrets belong in the secret manager.
2. Export each PostgreSQL database with a controlled backup identity (`pg_dump --format=custom --no-owner --no-acl`). Stream directly into the organization's approved authenticated encryption tool/storage. Do not write plaintext CV/database backups into this repository, CI artifacts or public cloud buckets. Verify command exit codes and ciphertext checksums; securely retain decryption keys separately.
3. Use the maintained storage provider's versioned replication/snapshot or S3-aware copy facility for CVs. Include **all retained versions**, not only current documents. Record object count and SHA-256 against `candidate_documents.ContentHash`. Objects referenced by old analyses must remain restorable. Protect backup manifests because keys/filenames may contain identifying information.
4. Restore into **new isolated databases/buckets/broker storage**, with outbound email and external AI disabled. Restore schema/data with error-stop enabled. Verify migration history, current-version uniqueness, old versions, analysis/review references, content hashes, offers and onboarding status. Reconcile Outbox/Inbox and pending/error queues before enabling workers; an empty broker plus restored DB alone is not a complete messaging recovery.
5. Run the synthetic business E2E and authorization regressions, record measured RPO/RTO, then approve traffic cutover. Do not overwrite live volumes during a rehearsal. Define the organization's acceptable RPO/RTO; no unmeasured values are claimed here.

An automated **Candidate DB + S3 restore rehearsal** runs in `CvPersistenceTests.DatabaseDumpAndS3RestorePreserveVersionsAnalysesReviewsAndContentHashes`. It uses only disposable synthetic source/target containers, actual pg_dump/psql, and S3 transfer; checks CV version history, one current version, analyses, separate human corrections, schema model and every restored object's hash. It does not certify full-platform/broker disaster recovery or backup encryption/key custody.

## Monitoring and bounded recovery

Export existing OpenTelemetry traces/metrics through `OTEL_EXPORTER_OTLP_ENDPOINT` to the operator's secured collector. Keep CV text, model prompts/responses, offer capability tokens and bearer tokens out of telemetry. Candidate readiness includes database/broker and document dependencies; liveness remains process-only. AI generation is optional and never gates Hiring.

Minimum alerts: repeated readiness failures, oldest pending Outbox/upload intent age, `_error` queue count, analysis queue age and expired leases, scan/OCR unavailability, exhausted AI quota/budget and disk pressure. Use aggregate metrics/static error codes, not CV samples. Investigate the first failing dependency and preserve failed messages. Fix the consumer/configuration, then replay reviewed messages with bounded batches and observe deduplication; do not purge error queues or synthesize business events to conceal loss.

## Local performance probe

`scripts/measure-read-load.mjs` measures authenticated **read-only local** Candidate requests with five warm-ups, bounded concurrency/request count and explicit p95/error gates. Provide a short-lived authorized development bearer token in `NEXUSHR_LOAD_TOKEN` (never in a CLI argument, source file, screenshot or report). Run `node scripts/measure-read-load.mjs`. It reports latencies/status counts only and rejects non-loopback targets. This small-data smoke test is not a production capacity benchmark; write contention, large datasets and sustained queue/model load require a representative isolated environment.

## Organizational decisions still required

Define actual retention/deletion periods, legal holds, access reviews, tenant/department boundaries, incident ownership, backup key custody and deployment approval. The present domain is a single-organization platform with explicit CV roles; it does not claim multi-tenant isolation. Do not add automatic deletion or broaden CV access in the absence of those decisions. Vision and automatic hiring/ranking decisions are outside this release.

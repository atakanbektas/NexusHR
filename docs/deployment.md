# Deployment and recovery

## Local operation

Run `docker compose up --build -d --wait --wait-timeout 300`. Diagnose with `docker compose ps` and `docker compose logs SERVICE`. Stop using `docker compose down` to retain named volumes. Do not delete data to fix schema or authentication errors. First startup imports the demo realm; existing Keycloak realm changes need explicit administration.

## Production requirements

Compose is a development topology. Inject PostgreSQL/broker/storage/SMTP credentials from an environment or secret manager. Remove demo users and restrict management/database ports. Configure trusted ingress, TLS, proxy topology and CORS. Supply HTTPS Keycloak authority/discovery, `Keycloak__RequireHttpsMetadata=true` and `Keycloak__Audience`; add a matching audience mapper to the client.

Use maintained object storage: archived MinIO is included for local reproducibility only. Validate current objects before switching endpoints. Frontend Vite variables are public build-time settings. `OfferResponse__PublicWebUrl` must identify the actual frontend origin.

Generate reviewed migration scripts using `dotnet tool restore`, then:

```sh
dotnet ef migrations script --idempotent --project NexusHR.Onboarding.Infrastructure --startup-project NexusHR.Onboarding.Api -o onboarding.sql
```

Repeat for Candidate/Hiring. Back up databases and storage, apply changes under a deployment identity, and start APIs with `Database__ApplyMigrations=false` (default outside Development). Prefer forward-compatible rollouts. The manual GHCR workflow publishes artifacts; it does not provision or deploy infrastructure.

For manual frontend publishing, configure the production environment variables `PUBLIC_API_URL`, `KEYCLOAK_URL`, `KEYCLOAK_REALM` and `KEYCLOAK_CLIENT_ID`. These are public build inputs, not secrets. Configure environment protection/approvals in GitHub before release publishing.

## Recovery

- CV failure: inspect/retry the existing draft. `cv_upload_intents` records unresolved compensation. Restore storage/database access and monitor reconciliation logs. Never delete an object merely because HTTP timed out.
- HTTP 409: reload the record and retry the intended transition; do not replay stale aggregate snapshots automatically.
- Missing downstream state: inspect `/health/ready`, Outbox age, endpoint/error queues and correlation logs. HTTP success may precede downstream visibility.
- Poison message: inspect schema, business IDs and exception in `<endpoint>_error`, fix the underlying cause and replay deliberately. Do not purge or fabricate events.
- Duplicate email: SMTP retries may resend the same offer; its token remains expiring and single-use.
- Telemetry: OTLP is optional. Export to a reachable, secured collector; business operations must not depend on it.

Alert on readiness failures, old Outbox/upload intents and error queues. HA, backups/restore drills, capacity, organizational data-access restrictions and load testing require deployment-specific acceptance.

# Staging deployment and release rehearsal

This guide is an executable configuration/release procedure, not evidence of a staging or production deployment. The application-only Compose topology is in `deploy/staging/compose.yml`. It does not reuse Development databases/volumes/users or expose DB/broker/scanner management ports. Provision test infrastructure locally or on an approved existing host; no cloud purchase is required.

## Prepare the boundary

1. Use synthetic candidates only. Approve the single-employer access matrix in ADR 007. DepartmentManager is an organization-wide reader in the current model; a department-restricted organization must not assume isolation exists.
2. Provision independent Candidate, Hiring, Onboarding and Keycloak PostgreSQL databases and a maintained S3-compatible service. Use separate application identities and a separate migration identity. The archived local MinIO build is a compatibility fixture, not a new production storage recommendation.
3. Provide HTTPS Keycloak authority/discovery, an explicit access-token audience mapper, TLS ingress for API/Web, trusted DB/S3/RabbitMQ certificates, and TLS SMTP or a private staging mail sink. Install private CA trust in the runtime images through the organization's approved image-build process; never disable certificate/hostname validation. The actual external CA/TLS deployment has not been executed here.
4. Rabbit transport now supports `RabbitMq__UseSsl=true`, defaults TLS to port 5671 and enforces TLS 1.2/1.3 with the configured broker hostname. Normal local Development remains on 5672. Use separate broker users with permissions tested against the required exchanges/queues. Restrict broker storage/management access: offer events contain sensitive bearer capabilities until delivery/retention cleanup.

## Per-service secret files (outside this repository)

Keep separate files for Candidate, Hiring, Onboarding, Notification and Gateway, readable only by the deployment operator (Linux mode 0600 or equivalent Windows ACL). Do not share all service DB credentials in one common env file. Never print resolved Compose configuration with secrets into logs/artifacts.

Each HTTP API/Gateway needs `Keycloak__Authority`, `Keycloak__MetadataAddress`, `Keycloak__ClientId`, `Keycloak__Audience`, `Keycloak__RequireHttpsMetadata=true`, and `Cors__AllowedOrigins__0=https://<staging-web-origin>`. Staging/Production reject insecure identity settings, missing audience, plaintext broker configuration and wildcard/path/non-HTTPS CORS origins. CORS is browser presentation policy; API authorization remains authoritative.

Candidate/Hiring/Onboarding need their own `ConnectionStrings__<Service>Database` using `SSL Mode=VerifyFull` and the approved CA setup. Business services/Notification need `RabbitMq__Host`, `RabbitMq__Port=5671`, `RabbitMq__VirtualHost`, `RabbitMq__Username`, `RabbitMq__Password`, `RabbitMq__UseSsl=true`. Candidate additionally needs `Minio__Endpoint`, `Minio__UseSsl=true`, bucket and least-privilege access keys. Notification needs SMTP TLS configuration and `OfferResponse__PublicWebUrl` pointing to the staging Web origin. Public URLs and Vite settings are not secrets.

The staging topology forces Candidate scanning on (private ClamAV plus separate signature updater), AI/external processing off and automatic migrations off. Existing local Ollama/OCR profiles remain available for local synthetic testing; staging does not silently enable external AI or download/invoke a model. ClamAV signatures still need scheduled updates and monitoring. The existing nginx master requires a small documented capability set; Web is not claimed to be a non-root image.

## Reviewed release and migration

1. Review and merge the relevant PRs explicitly; do not publish unreviewed branch commits. The manual publish workflow now requires a successful **main push CI for the exact SHA**, uses the selected protected staging/production environment, labels images with revision/source and records immutable registry digests. No publishing/deployment was executed for this hardening task. Configure environment protection before release.
2. Build Web with the staging environment's public API/Keycloak origins; Vite settings are compiled into the image and cannot be changed with runtime environment variables. Preserve the six image digests/revision manifest as one release candidate.
3. Quiesce writers and take encrypted recovery snapshots before applying migrations. Generate reviewed idempotent SQL using `dotnet ef migrations script --idempotent` for each service, matching the release revision. Apply with a controlled migration identity and error-stop behavior. The new Candidate sort index can lock writers during creation; schedule and measure a maintenance window for large data. Startup application identities do not need schema-change privileges.
4. Set `CANDIDATE_IMAGE`, `HIRING_IMAGE`, `ONBOARDING_IMAGE`, `NOTIFICATION_IMAGE`, `GATEWAY_IMAGE`, `WEB_IMAGE` to full `registry/name@sha256:<64 hex>` references, and `<SERVICE>_ENV_FILE` to absolute private per-service paths (no Web env file). Then run from the repository root:

```sh
node scripts/validate-staging.mjs
docker compose -f deploy/staging/compose.yml pull
docker compose -f deploy/staging/compose.yml up -d --wait --wait-timeout 600
```

`validate-staging.mjs` checks immutable image-reference syntax, files outside the repository and Compose validity; it does not prove certificates, credentials, image provenance or runtime readiness. CI validates it with empty temporary external files and dummy digests, without deployment.

The validator resolves symlinks/canonical paths, requires private Unix file permissions (Windows ACL remains an operator check) and suppresses Compose parser details that could echo a malformed secret value. A failed validation is still a nonzero exit; inspect the private configuration locally.

## Accept and roll back

Terminate HTTPS at approved ingress and forward to loopback Gateway 5500/Web 5501. Do not expose the internal service ports. Verify real OIDC issuer/audience/roles, invalid-role and CV-denial cases, dependency readiness, ClamAV failures, one synthetic complete business workflow and queue/error state before acceptance. Monitor trace/correlation IDs, HTTP 5xx/latency, broker/outbox age, scan failures and signature freshness through the existing OTLP exporter and operator collector. Do not enable payload/body logging.

For application rollback, select the prior **complete digest manifest** and restart the application-only topology after checking schema compatibility. Do not run destructive down migrations, purge queues or overwrite database/object volumes. CV history migrations intentionally refuse lossy rollback; prefer a reviewed forward repair. If restored data is required, restore into a new isolated environment and validate all references and messaging before cutover. See [operations](operations.md) and [recovery evidence](audits/recovery-performance-readiness.md).

Offer-link rollout order matters: deploy fragment-aware Web **before** Notification begins sending `#token=` links. Web keeps accepting existing `?token=` links. Rolling Notification back is compatible with the new Web; do not roll Web back to a pre-fragment version while unexpired fragment links exist. Retain the compatible Web digest or forward-fix that parser. New capabilities no longer enter initial HTTP queries; sanitized frontend access logs additionally protect old links. Do not treat access-log redaction as proof that every third-party transport/error collector is safe without testing that deployment.

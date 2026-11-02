# Reproducible local read/write load testing

The k6 suite uses real JWT authentication, Gateway, Candidate/Hiring APIs, PostgreSQL and RabbitMQ in a separate synthetic fixture. It never sends a CV to any provider and does not use business DB/object/broker volumes. Keycloak/Web are used only for actual login; the login helper supplies empty UI API responses to avoid reading business data. All measured API requests go to the independent fixture on loopback port 5400.

## Run

Use Docker Compose >=2.24.4, .NET 10, Node 24/npm and installed Playwright Chromium. Keep the normal local Keycloak/Web available. The load Compose file inherits clearly marked Development fixtures and deliberately disables AI/OCR/document scanning because this suite exercises contact CRUD/listing, not document processing. Never reuse these settings for staging/production.

```powershell
# From the repository root, build the isolated Candidate/Gateway images.
docker compose -f docker-compose.load.yml build candidate-api gateway
docker compose -f docker-compose.load.yml up -d --no-build --wait --wait-timeout 300
$env:E2E_USERNAME = Read-Host 'Authorized synthetic-development username'
$env:E2E_PASSWORD = Read-Host -MaskInput 'Password'
node scripts/load/run.mjs .tools/load-run
```

Use an approved synthetic-development account with Candidate/Hiring read and Candidate edit access. The runner obtains a JWT through real OIDC/PKCE, keeps credentials/token in memory/environment only, uses a digest-pinned k6 2.3.0 container and writes numeric metrics under ignored `.tools`. Do not copy browser tokens or benchmark credentials into GitHub/issues/logs. A failed threshold returns a nonzero process exit; do not hide it.

Default workload: seed 4000 synthetic drafts through the API (batches of 20), then 20 read VUs and 4 create/update VUs for 30 seconds, plus up to 15 seconds graceful completion. Reads select different 20-row overview pages; writes create a unique draft and update its contact fields. The suite never verifies candidates, sends offers/email, deletes records or invokes AI. Bounds: 100..10000 seed rows and 15/30/60/120-second runs. `LOAD_SKIP_SEED=1` reuses an existing synthetic fixture only after checking its count. `LOAD_SEED_COUNT` and `LOAD_DURATION` configure bounded larger runs.

Record separate runs in separate `.tools/load-*` directories. Capture `docker stats --no-stream` for fixture containers and `docker info` during execution; these are point samples, not resource peaks. Stop with `docker compose -f docker-compose.load.yml stop`. Do not remove business volumes or use `down -v`. Fixture containers/data remain available for inspection and deliberate operator cleanup under their own retention policy.

## Comparison protocol

Archive an explicit baseline SHA into an ignored directory, build its Gateway Dockerfile into a distinct image tag, and set `LOAD_GATEWAY_IMAGE` to that tag for an independent Compose project such as `nexushr-load-before`. Stop the other fixture first because both intentionally use loopback port 5400. Use separate fresh synthetic DB/broker/object stores for each project, same Candidate/Hiring images, seed count, VUs, duration and host resources. Never reset a running shared database to create a benchmark.

This isolates the Gateway composition change; it does not isolate the new Candidate index's contribution. Dataset sizes grow during writes. Counter rates in the k6 summary include setup/graceful time; report counts, configured windows and latency percentiles together. One local closed-workload pass is not a capacity, endurance or production throughput guarantee. Repeat under representative data distributions/hardware and approved staging resource limits before setting service SLOs.

Observed baseline/new metrics, resource context and unsuccessful baseline thresholds are in [the execution report](audits/recovery-performance-readiness.md). The primary experiments used distinct fresh 4000-row fixtures. The old Gateway failed the unchanged 1000ms read-p95 threshold; the new Gateway passed. No scenario was dropped to obtain that result.

References: [k6 scenarios](https://grafana.com/docs/k6/latest/using-k6/scenarios/), [thresholds and nonzero exits](https://grafana.com/docs/k6/latest/using-k6/thresholds/).

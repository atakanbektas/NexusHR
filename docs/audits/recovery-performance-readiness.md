# Recovery, performance and readiness execution report

Date: 2026-11-02. Base main `045630a`; application follow-up is PR #36 (`ef32f11`), operational changes are a separately reviewed branch. Main is not merged/changed by this task. Existing business databases, named volumes, CV/analysis history and local AI capabilities remain intact.

## Measured changes

Read [the initial findings](engineering-hardening.md) and ADRs 007/008. Application changes reduce normal Gateway composition to one Candidate page plus one Hiring batch, bound cross-service filters and stabilize/index pagination. Operational changes add migration-aware readiness, strict staging identity/CORS/broker configuration, certificate-validating RabbitMQ TLS in all adapters (including explicit port support in Hiring/Notification), per-service external secret files, exact-main-CI release gating, OCI revision/source labels and image-digest manifests. Publishing is still manual, environment-protected and does not deploy.

A final privacy review reproduced a real frontend leak: nginx's default request/referrer access format included offer query capabilities. The native regression failed against the original config and passed with metadata-only JSON access logging. New offer emails use `#token=...` so the capability is never part of the initial HTTP request; the frontend still accepts old query links. Existing token hashing/expiry/one-use/concurrency and POST response contracts are unchanged. Actual mail-to-browser acceptance and legacy-link compatibility are verified separately; do not call a mocked legacy UI response proof of API authorization.

## k6 evidence

Executed k6 2.3.0, pinned `grafana/k6@sha256:3e4186aea62e11a45560bfc51e19eeafe82fc07ce069c78aff44b7ff893d9a5e`. Each primary experiment seeded an independent fresh 4000-draft fixture through actual authenticated APIs, then ran 20 read VUs + 4 create/update VUs for 30s plus graceful completion. Same Candidate/Hiring images and hardware; baseline Gateway was built from archived main `045630a`, new Gateway from the hardening tree. The new fixture ran first; the old one ran second. No business store was reused or reset. Scanning/AI were intentionally off in this CRUD/list workload, not removed from the application.

| Metric | Baseline Gateway | Hardened Gateway |
| --- | ---: | ---: |
| Read p50 / p95 / p99 (ms) | 1050.15 / 2344.60 / 2465.46 | 13.46 / 67.73 / 120.61 |
| Write p95 / p99 (ms) | 62.49 / 111.10 | 59.33 / 130.15 |
| Completed read operations | 459 | 4781 |
| Completed write HTTP operations | 924 | 1020 |
| k6 read counter rate, includes setup/graceful time | 10.81/s | 126.73/s |
| k6 write counter rate, includes setup/graceful time | 21.76/s | 27.04/s |
| Scenario error rate | 0% | 0% |
| Unchanged read p95 <1000ms threshold | **Failed** | Passed |

This is a local single-pass observation, not a production throughput/SLO/endurance guarantee. Writes grow each dataset; counter rates include different setup durations. No write-p99 improvement is claimed. The structural reduction in downstream work is independently regression-tested. Candidate index impact was not isolated because both experiments used the same Candidate image. The early harness attempt failed before workload execution because local Web was stopped; it is not a benchmark result.

Docker VM: 12 CPUs, 16,671,076,352 bytes RAM (~15.53 GiB), shared with local development/verification tasks. Point samples (not peaks): baseline Gateway 121.80% CPU /136.1MiB; Candidate 108.86% /173.6MiB; Hiring 296.04% /126.4MiB; Candidate DB 395.21% /91.87MiB. A separate later 15s same-fixture probe exercised the committed runner and sampled the new Gateway 43.73% /75.49MiB, Candidate 125.75% /167.3MiB, Hiring 33.05% /160.2MiB and Candidate DB 28.53% /58.22MiB. The later probe reused its larger synthetic dataset and is not substituted into the primary comparison. Sampling is too sparse for peak/capacity conclusions. Excess whole-dataset HTTP/DB work was the confirmed bottleneck; realistic staging concurrency/data/retention distributions still require evaluation.

Reproduction and workload budgets: [load-testing.md](../load-testing.md). Ignored raw numeric summaries/resources remain under `.tools/load-*`; no tokens, CVs or personal response bodies are committed.

## Actual recovery evidence

- Existing retained-CV rehearsal: real pg_dump/psql into a new PostgreSQL target, separate S3 transfer, retained revisions/reviews/hashes checked.
- New cross-service rehearsal: Candidate/Hiring/Onboarding databases dumped only after fixture writers quiesced, restored into a different PostgreSQL server, completed business links/eligible projection/task state retained. Referenced valid PDF copied into a different bucket, hash checked against restored metadata and reopened with PdfPig. Same test S3 engine for those two buckets; the earlier CV rehearsal separately tests different storage containers.
- New broker rehearsal: durable persistent message and topology cold-backed up, then restored onto an independent same-version/same-named node. A definitions-only restore would fail the payload assertion. Cookie/data stay in disposable fixture memory/containers, never repository/CI artifacts. This is single-node recovery, not cluster HA.
- New identity rehearsal: actual Keycloak 26.7.0 uses a source PostgreSQL database, authenticates a synthetic user, stops, dumps/restores into a new DB and starts a new server **without realm reimport**. Login, realm role, client/audience mapper and signing-key ID survive. Password grant is enabled only inside this synthetic test realm; no production realm/auth implementation is changed.
- Existing real broker outage/dedup/outbox test additionally checks bounded poison-message failure into `_error`, absence of partial state and subsequent valid processing.

These drills test components and cross-database references; they do not certify a coordinated timed whole-platform traffic cutover, encrypted offsite custody, cert rotation or cluster failover. Proposed RPO<=24h/RTO<=4h are owner-approved targets still to be measured, not inferred from fixture duration. See [operations](../operations.md).

## Verification and limits

- Baseline: build zero warnings/errors; 173 ordinary backend tests passed, 15 explicit opt-ins skipped. Application milestone: 188 passed. Operational core: 201 passed; native nginx privacy test additionally passed. Final combined run/CI status is recorded on the PR.
- Three-database/S3 and cold broker restore tests passed; identity restore passed in 64s. Its first attempt failed on Keycloak's required realm filename convention; corrected the filename rather than weakening assertions. New-test missing imports/configuration-binder API compilation errors were corrected before final build. An overlapping Windows log-file write was rejected; subsequent runs used serialized builds and separate output files.
- Business/security browser cases passed before the new fragment URL change; final actual-mail fragment acceptance, legacy compatibility and latest-head CI are explicitly recorded on the PR after execution. Five deterministic-provider AI browser cases passed. Real paid AI calls: none. Current model/prompt/extractor/evidence/provider behavior is preserved; broad Qwen recall/accuracy was not rebenchmarked in this task. Fragment-aware Web must precede new Notification links and remain available during rollback until those links expire (staging guide).
- npm ci/lint/build passed; entry bundle ~195.6kB. **12 moderate development-tool findings** derive from minimatch GHSA-g3hv-75wg-m249; npm's proposed fix forces an unsupported toolchain downgrade/major change, so it was not blindly applied. Runtime npm audit: zero; no npm High/Critical findings. NuGet High/Critical gate: zero. This is not a claim of a complete container-image advisory scan.
- All three migration-drift checks passed. Staging/optional/load Compose validation passed; staging parser tests use empty external temporary files and dummy digests only. An isolated nginx runtime accepted the staging capability subset and returned HTTP 200. actionlint 1.7.12 and Node syntax checks passed. Release publication, real staging deployment, private CA/TLS handshake validation and production deployment were **not** executed.

## Weighted assessment

This rubric grades engineering evidence, not the earlier subjective portfolio-presentation score. Scores are reviewer judgments with stated limits; test counts alone do not establish correctness.

| Category | Maximum | Before | After | Evidence / deduction |
| --- | ---: | ---: | ---: | --- |
| Architecture | 20 | 18 | 18 | Preserved boundaries, dependency guards and explicit scope decisions; existing result/controller duplication remains |
| Backend engineering | 20 | 17 | 19 | Bounded composition, stable indexed ordering, error/cancellation handling; legacy unpaged Onboarding list remains |
| Distributed systems | 15 | 12 | 14 | Real outage/dedup/poison and cold message restoration; SMTP acknowledgement ambiguity remains |
| Testing and reliability | 15 | 12 | 14 | Executed red/green security/performance regressions and actual component restore/browser checks; no full endurance/cutover certification |
| Security | 10 | 7 | 8 | Explicit resource binding, privacy fixes, strict staging/TLS options; department-bound deployments and dev-tool advisory still require action |
| DevOps and observability | 10 | 8 | 9 | Exact-revision CI release gate, digests/labels, schema readiness, safe logs and staging validation; publish/CA deployment acceptance unexecuted |
| Frontend quality | 5 | 4 | 4 | Compatible routes/legacy offers and small bundle retained; broader accessibility/device matrix not certified |
| Production readiness | 5 | 3 | 4 | Real DB/object/broker/identity component recovery plus staging/rollback procedures; full coordinated cutover, maintained storage and target RPO/RTO remain acceptance gates |
| **Total** | **100** | **81** | **90** | Target applies to demonstrated repository engineering, not production approval |

No critical vulnerability was proven that justifies a tenant/microservice rewrite. High context-dependent residual: organization-wide reader roles are unsuitable where department isolation is mandatory; implement authoritative assignment/membership/backfill first. Also retain explicit work items for Onboarding pagination, transport/error-log privacy review in the actual collector, compatible minimatch tooling fixes, maintained S3 migration, broad CV accuracy/endurance and approved production secrets/PKI/retention. No placeholder implementation, merge or production deployment is used to increase this score.

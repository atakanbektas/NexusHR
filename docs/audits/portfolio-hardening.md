# Portfolio hardening verification — 2026-10-30

Branch: `codex/portfolio-hardening`, based on `e0c0099` (PR #25). The PR stack is #23 → #24 → #25 → this change. No main commit, automatic merge, production deployment, database reset or volume deletion is authorized/performed.

## Delivered

- Additive EF migration; immutable CV versions, current-version conditional replacement, retained source downloads, PostgreSQL revision/current uniqueness and xmin races, durable MinIO compensation.
- PdfPig-first local OCR for scans/images, Turkish/English Tesseract, page/word provenance, bounded native subprocesses and cancellation cleanup. No vision model or raw image provider transmission.
- Independent private ClamAV adapter; fail-closed upload/read/extraction, production startup guard and document dependency readiness. Development opt-out is explicit.
- Grounded structured employment/education/language fields; child evidence must belong to its parent quote. Separate append-only HR corrections; source version preview and useful category tables. Date-range calendar coverage never guesses missing dates.
- Regression fix: contact minimization had mistaken contextual ISO employment date ranges for phones. Known identifiers and contact-labelled dates remain masked; offsets stay stable.
- Lazy frontend routes (largest bundle/entry approximately 196 kB), synthetic screenshot, operation/recovery runbook, real Candidate PostgreSQL/S3 restore rehearsal and bounded read-load script.

## Executed verification

| Check | Result |
| --- | --- |
| .NET 10 build | Passed, zero warnings/errors after final prompt update |
| Normal solution tests | 173 passed; 15 opt-in cases skipped in the normal run (native/browser cases run separately) |
| Native OCR | 3 passed: five document formats with TR/EN, corrupt/blank/low-quality input, cancelled native-worker cleanup |
| Real ClamAV | 1 passed: clean PDF accepted, harmless EICAR decoded only in memory rejected |
| AI browser regression | 5 passed: analysis/review, contact-only and professional-empty results, disabled OCR explanation, structured tables/version replacement/human correction |
| Candidate → Hiring → Onboarding browser | 2 passed, including real Keycloak, email acceptance, task completion and auditor upload denial |
| npm ci / lint / build | Passed, npm audit at install reported zero vulnerabilities |
| Scoped restore rehearsal | Passed: actual pg_dump/psql restore into new disposable PG, retained CV bytes copied to new isolated S3, versions/reviews/hashes checked |
| Local read probe | 500/500 HTTP 200, zero errors; 5 workers, p50 7.74ms, p95 13.75ms, p99 17.54ms, 43.42 requests/sec. Read-only synthetic/small-data local probe, not production capacity certification |
| Compose all optional profiles | All optional profiles validate; rebuilt local API/Web/OCR/scanner healthy |
| Actual Qwen OCR workflow | Initial run failed its employment assertion: five grounded facts, job misclassified as duration, fields omitted. Required fields array and precise category prompt fixed this; real-model rerun passed (2m11s), including three professional categories, structured fields, OCR source regions, zero API cost and idempotent replay |
| EF model drift | No pending model changes in Candidate, Hiring or Onboarding |
| Dependency audits | npm: zero vulnerabilities; NuGet: no High/Critical findings |
| Native Qwen regressions | Four passed before required-field prompt change; final TR/EN rerun in progress |
| GitHub Actions | Pending push/PR |

## Observed failures and corrections

The initial structured browser regression caught the date minimization defect above. A subsequent test selector clicked a different fact's correction button; each button now identifies its fact index. Initial OCR packaging mixed Python ABIs; the helper now uses Debian's Python/Pillow together. An unquoted Compose tmpfs comma produced an invalid mount and was corrected. A parallel Windows build hit a DLL lock; builds/tests are now serialized. These failed attempts are not counted as passes.

## Remaining acceptance

This repository does not merit an unconditional 100/100 or a production certification. Confirm organizational role/department/tenant isolation, retention/erasure/legal policies, maintained S3 service, real secrets/TLS/network ingress, protected release environments and full-platform encrypted recovery/RPO/RTO. Candidate DB/S3 recovery does not certify broker/identity/Hiring/Onboarding disaster recovery. Local OCR may misread and Qwen may omit fields; evidence proves text alignment rather than factual truth. Use human review and larger representative language/layout datasets before relying on completeness. No paid AI call or real personal CV processing was used for this hardening verification.

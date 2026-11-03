# Engineering Control Center verification — 2026-11-03

Scope: dedicated `codex/engineering-control-center` branch; no main merge or production deployment. Measurements below are local observations, not production capacity guarantees.

## Executed checks

| Check | Result |
| --- | --- |
| .NET 10 solution build | Passed, zero warnings/errors |
| Full ordinary backend suite | 225 passed, 0 failed, 15 opt-in skips |
| Platform suite after malformed-agent-report regression | 36 passed, 0 failed (35 included in the ordinary suite; one additional regression) |
| Candidate/Hiring/Onboarding EF model drift | All three models match migrations |
| npm ci / lint / production build | Passed with Node 24 |
| Node lab-boundary and benchmark-cancellation tests | 7 passed |
| Ordinary business Playwright | 3 passed: onboarding/offer flow and authorization/legacy offer checks |
| Engineering reader browser | Passed against actual Keycloak, telemetry, PostgreSQL, RabbitMQ and candidate authorization |
| Engineering load browser | Passed: real 5-VU k6, JSON/CSV downloads and durable cancellation |
| Capacity / fault browser | 5 passed: observed SLO breach, Candidate restart, broker outage, database outage and injected latency |
| Real bilingual synthetic model benchmark | Passed with both cached models; durable source-validated facts and UI history |
| Actual OTLP ingestion → Tempo/Loki storage | Passed; synthetic secret body, URL, resource, event and status contents absent |
| OCR / ClamAV / deterministic AI browser opt-ins | 8 passed initially, 1 Keycloak timeout; that test passed separately after services stabilized |
| Scanned PDF → real OCR → real Ollama → grounded persistence | Passed with a separate loopback-only test inference server and read-only cached weights; server stopped afterwards |
| Compose configuration / actionlint | Passed for engineering, isolated lab and complete optional overlays |
| Dependency audits | NuGet: 0 advisories; npm: 12 moderate, 0 high/critical (development tooling) |
| Generated-secret scan | No generated private credential found in repository source files |

The ordinary suite plus the additional platform regression verifies 226 distinct backend cases. Tests are not counted twice when repeated. Opt-in model-corpus and standalone provider tests were not all rerun; a model benchmark does not imply those tests passed.

## Measured model benchmark

Fixed public synthetic bilingual PDF, actual Chromium PDF → PdfPig → privacy minimization → Ollama → source evidence validation → PostgreSQL. Separate RAM-only lab, cached weights mounted read-only. Main AI settings remain unchanged. No paid/external AI calls.

| Model | Validated facts | Literal gold markers | Attempt duration | Native output rate |
| --- | --- | --- | --- | --- |
| qwen3:4b | 5 | 4/4 | 95.70 s | 7.52 tokens/s |
| qwen2.5vl:3b | 5 | 4/4 | 79.37 s | 12.86 tokens/s |

Four literal markers on one synthetic input are a smoke benchmark, not broad extraction accuracy. Evidence validity does not imply every fact was extracted or independently true.

The first benchmark attempt failed while dependency installation overlapped the running browser/agent: it recorded an execution failure, and its model attempt recorded `AI.Timeout / Provider`. It is retained in history. After dependency installation completed, an isolated rerun succeeded for both models. The original failure is not relabeled or hidden. Subsequent cleanup hardening waits for terminal model-job cancellation and restoration before accepting cleanup; three deterministic regression tests cover terminal preservation, latest-version cancellation and unsettled jobs. A further real-agent rerun of that last cleanup change is not yet verified: automatic command review rejected the background agent restart, without a detailed reason. This gate remains open.

One deterministic AI browser case timed out waiting for Keycloak during container refresh; it passed on the targeted stable-stack rerun. The combined OCR/Ollama test initially returned 503 because its required loopback inference test server was absent. After starting a bounded loopback-only test server using read-only cached weights, the actual scanned-PDF/OCR/model/grounding/persistence test passed in 81 seconds. The test server was then stopped; no business volume was changed.

## Preservation and limitations

Final preservation snapshot retained all baseline identifiers/checksums: 62 candidate/document entries, 13 hiring entries, 52 onboarding/task entries, 25 identity entries, 30 physical object-file checksums and 8 business volume names. Technical accounts and metadata roles were added explicitly; existing accounts were not reset. New engineering telemetry/history volumes are separate. The only business schema change adds nullable AI-attempt metadata columns and an index.

No arbitrary EXPLAIN/SQL, broker payload reads/replay, raw exception messages, causal critical-path guarantee, parsed CI artifact test/vulnerability totals, workflow/deployment writes or production capacity certification is implemented. These are documented constraints rather than fabricated dashboard values. CPU/host metrics describe Docker's Linux VM; shared-host load results include interference. Missing metrics remain unavailable. Manual lab GitHub workflow execution and final PR CI outcomes must be checked on GitHub separately from local results.

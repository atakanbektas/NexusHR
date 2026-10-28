# Docker Ollama verification

Date: 2026-10-28. Branch: `feature/docker-ollama-integration`, based on `feature/ai-cv-analysis` at `9bb3005`. PR #23 was open at branch creation; this change is stacked onto that AI branch, not directly onto main.

## Real local result

The full application browser test created a synthetic Turkish developer PDF from the committed HTML fixture, used the existing extraction/registration/upload form and requested analysis through Candidate API, PostgreSQL Outbox, RabbitMQ Inbox and the worker. The actual local model produced **18 grounded facts** covering all six categories. Skills included C#, .NET, ASP.NET Core, SQL Server, PostgreSQL, Docker, React, TypeScript, RabbitMQ and Clean Architecture.

Extractive summary: “Kurumsal web uygulamaları geliştiren, test ve mesajlaşma odaklı yazılım geliştirici.”

Candidate: `44f420de-7f36-4bde-a722-63d23436eb02`; analysis: `70bf52f8-0c2a-40f3-ae5a-9abf8ab07f37`. These are synthetic test records, preserved in the local development database. SQL verified `Ollama|Succeeded|0.00000000|0.00000000|approve`. History, literal source evidence, HR review, and model/application state after restarting both Ollama and Candidate API were verified. The browser workflow took about 2.6 minutes on CPU in this environment; this is an observation, not a latency guarantee.

Runtime: official Ollama `0.40.12`, pinned image manifest `sha256:20d1bbfbabe1c97ba4b16301f08a20d8484bcf77047742a3cb71c613d8e12d0b`. Model `qwen3:4b`, Q4_K_M, model manifest `359d7dd4bcdab3d86b87d73ac27966f4dbb9f5efdfcc75d34a8764a09474fae7`. The same digest is stored in the attempt provider reference. Observed inference memory was about 5 GiB with roughly four CPU cores; Docker had about 16 GiB/12 CPUs allocated. External AI API cost was zero.

## Checks executed

| Check | Evidence |
|---|---|
| Release solution build | Passed, 0 warnings/errors |
| Candidate unit tests | 23 passed |
| Hiring unit tests | 31 passed |
| Candidate HTTP/PostgreSQL/MinIO/RabbitMQ/provider contract tests | 62 passed; 4 opt-in browser/native tests deliberately deferred |
| Onboarding regression | Initial loaded run 13 passed/1 timeout; isolated unloaded recheck 14/14 passed |
| Targeted AI reliability + Ollama HTTP contracts | 37/37 passed; overlapping cases, not extra independent test counts |
| Native tests against the new isolated Docker model through the loopback test proxy | 3/3 passed: grounded Turkish inference, missing/stopped server, cancellation after preflight |
| Normal Playwright | 3 passed: AI-off independence, full Candidate → Hiring → Offer Acceptance → Onboarding completion, auditor write denial; 2 opt-in cases deferred |
| Dedicated fake-provider browser host | Passed; existing normal CI scenario preserved |
| Real Ollama Playwright | Passed: Turkish PDF upload, six categories/evidence, PostgreSQL persistence, history, HR review, model/API restart |
| npm ci, lint, frontend build | Passed; existing >500 kB bundle warning remains |
| npm and NuGet transitive audits | No reported vulnerabilities |
| Candidate/Hiring/Onboarding EF model drift | Passed, no extra relational migration needed |
| Default, AI and AI-test Compose configurations | Passed |
| Docker readiness | Existing stack and new isolated inference server healthy; test proxy stopped/removed after testing |
| Privacy checks | No OpenAI URL or synthetic CV summary found in Candidate API logs; local adapter selected without fallback; inference server has only the internal network and no published port |

There are 130 distinct ordinary backend regression cases, plus three opt-in native model cases. The fake browser wrapper is counted with the browser scenario, not as independent extra backend coverage. There are five distinct browser scenarios including the real model path. Initial failures were not counted as passes.

## Resolved environment findings

An independent pre-existing `nexushr-ollama` occupied host port 11434. It and its data were preserved. Initial native checks on that port are not used as proof of the new isolated service. Final native verification used the new service through loopback 11435. Docker Desktop did not publish a usable host mapping for the internal-only network; the explicit test-only proxy fixes host test access without attaching the inference server to an Internet-capable network. An early browser attempt during this network transition correctly kept submission disabled; the stable-network rerun passed.

Running CPU inference concurrently with infrastructure regression tests caused the existing Onboarding message-delivery timeout. The model was unloaded (not deleted), then all 14 Onboarding cases passed. Normal CI does not run inference; model validation is an explicit separate workflow. Native CPU tests are intentionally run separately from heavy regression/build workloads.

## Remaining limits

This validates one synthetic Turkish CV plus native synthetic text, not population-wide quality, fairness or production accuracy. GPU operation and the newly added manual GitHub real-model job were not executed here. Standard PR CI is tracked separately on the final head. CPU speed, memory pressure and model/context limits can cause controlled failures. Exact evidence validates source location, not the truth of a CV. Retention/deletion governance, malware quarantine and broader production controls remain required before production use.

The existing separate host-published Ollama server is outside this profile and was not reconfigured. The new application routes to the private `nexushr-local-ai` alias and does not use that server. Existing candidates, databases and named volumes were not reset or deleted.

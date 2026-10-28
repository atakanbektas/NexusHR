# ADR 005 — Local Ollama with provider-specific accounting and literal evidence

Status: implemented and verified with synthetic data on CPU.

The existing Candidate-owned async job, EF Outbox/Inbox, lease, idempotency, xmin, history and human-review boundaries remain. `CvAnalysis:Provider` selects exactly one HttpClientFactory adapter. OpenAI remains the default; Ollama is an opt-in local adapter. Failed local inference never falls back to a paid or third-party provider.

Local calls consume request quota and bounded parallel CPU work but reserve no paid API budget. Attempt USD usage is zero, including timeout/cancel/recovery. OpenAI reservation, pricing and unknown-outcome semantics remain. Provider/model changes cannot redirect an old run to a different adapter. An installed-model readiness check runs before queuing/reserving, outside DB transactions; history exposes readiness so the UI disables submission while unavailable. Idempotent acknowledged replays remain readable during an outage.

Ollama uses native `/api/chat` with schema format, nonstreaming/nonthinking output and bounded context/output. Its wire facts contain category/value/page/literal quote. The adapter derives the first exact source occurrence, then runs the existing page/span/value validator. There is no fuzzy matching, JSON repair or invented missing field. This avoids trusting small models to count UTF-16 offsets while preserving authoritative source checks. The professional summary is also extractive evidence. `summary` is an additive fact category; stored v1 results and OpenAI wire format remain compatible. No relational migration is required.

A bootstrap container downloads model assets into a dedicated named volume and exits. It never handles CVs. The inference daemon has only an internal Docker network, no published port and cloud support disabled. The Candidate API retains its existing service network and joins the internal AI network via the unique `nexushr-local-ai` alias. This avoids an observed conflict with a pre-existing independent `nexushr-ollama` on host port 11434, which is preserved untouched.

Endpoint validation allows only the designated local service aliases or loopback; redirects/cloud-backed models are rejected. Native host tests use an explicit loopback-only 11435 proxy because Docker Desktop does not publish ports from this internal-only network. That test-only proxy allows version/tags/show/chat, has access logging disabled and is absent from the normal local-AI deployment.

CPU is the supported baseline. Profile limits are 8 GiB/6 CPUs, four model threads, one inference per instance and two attempts maximum. Local cancellation observes the durable cancellation flag and cancels the HTTP call; interrupted local leases become retryable Failed records with zero API cost. Paid-provider behaviour is preserved.

Consequences: optional first-run image/model downloads, slower CPU inference, model-quality dependence, bounded local resource consumption and an explicit opt-in real-model validation job. Normal CI remains deterministic and does not download a model. GPU acceleration and production data retention/governance remain separate work. See [PowerShell setup, controls and tests](../local-ai-ollama.md).

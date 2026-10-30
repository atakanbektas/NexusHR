# Ollama evidence failure investigation — 2026-10-30

## Scope and preserved work

Fix branch `codex/fix-ollama-evidence` starts at PR #24 head `923e911`. PR #24 targets open PR #23's `feature/ai-cv-analysis`; this fix targets PR #24's branch. Neither PR is merged. Unfinished multimodal work is preserved separately in the local `codex/local-multimodal-cv` branch's named WIP stash and is excluded. No OCR, vision model, schema/category expansion or document versioning is part of this repair.

## Proven failure

The latest failed local records had `AI.InvalidResponse`. Their original raw model output was **not retained**, so historical bytes cannot be reconstructed. Safe input logs showed readable input reaching the worker, without a completed output metric. A read-only local check confirmed stored/downloaded file length and hash agree. No CV text, identity, object key or hash is included here.

The **same stored two-page PDF** was reproduced using the existing Linux container runtime, PdfPig, unchanged contact minimization, unchanged native prompt/JSON Schema and actual Docker `qwen3:4b`. Both pages retained readable professional text (762 and 376 letter/digit characters after minimization). The complete model response was valid JSON, had `done=true`/`done_reason=stop`, and proposed 22 facts with existing schema fields, recognized categories and valid page numbers.

Four quotes had no literal match but did match after whitespace-only canonicalization. Three other fact values were not literal substrings of their quotes. Fifteen facts already met both literal conditions. The old adapter's `Select(...).ToArray()` threw on the first absent quote, discarding every valid sibling. Its broad catch collapsed this evidence failure into `AI.InvalidResponse`. Thus this reproduction failed **after generation, at evidence resolution**, before result persistence. The PDF/minimization were not empty, the schema/page numbers were not the failure, and the model was already not being asked for offsets.

No real model response text was saved. Temporary diagnostics held it in memory and emitted only character/token/fact counts, boolean validation results and static rejection categories.

## Repair and invariants

- Keep the native prompt, model, schema and final `EvidenceValidator` unchanged.
- Resolve each proposed quote against the declared page. Require a unique exact match. If exact matching fails, allow only deterministic whitespace-run canonicalization, with a UTF-16 source-position map. Reject ambiguous exact or canonical matches. Do not alter case, spelling, punctuation, accents, word order or meaning; no embeddings, fuzzy matching or guessed offsets.
- Restore the **original source substring** and original UTF-16 offset before validation/persistence. A value must still be in that quote; whitespace-only restoration of a value is confined to that quote. Never search another page or a different passage for a missing value.
- Reject malformed/unsupported individual facts, wrong pages, missing/ambiguous quotes and unsupported values individually. Deduplicate identical resolved facts. A fixed Turkish warning reports omitted fact count without retaining rejected content. JSON/envelope corruption, invalid usage and unbounded shapes remain failures.
- Revalidate all accepted facts with the unchanged validator and again in the worker. One or more valid facts may produce `Succeeded`; zero valid facts produces `NoFindings`/`AI.NoSupportedFacts`. Invalid JSON/response envelopes remain `Failed`. Preserve existing external-outcome/budget semantics.
- Log only accepted/rejected/whitespace/duplicate counts and static stage codes. No raw response, input, quote, warning body or identity is logged.
- No changes to roles, redaction, review, idempotency, retries, cancellation, budgets, Outbox/Inbox or business workflow. No migration and no historical record rewrite.

The fixed adapter was rerun on the same stored PDF in Linux: **22 proposed, 21 accepted, 1 unsupported value rejected, 3 whitespace restorations**, with summary, employment, education, certifications, language and skills represented. Actual model reruns can vary even with seed/temperature settings; this is not a claim that model output is byte-for-byte deterministic. The backend grounding is deterministic.

## Verification protocol

Eight regressions first failed against the original code, covering sibling poisoning, whitespace restoration, ambiguous exact/canonical matches, wrong pages, unsupported values, case changes and malformed/duplicate siblings. The corrected adapter/contract group passed 23 cases. PostgreSQL worker tests additionally verify durable partial success, all-rejected `NoFindings`, idempotent replay and one attempt.

The opt-in `ollama-corpus.spec.ts` generates five Turkish and five English realistic synthetic PDFs with Chromium. It uses actual Keycloak/Gateway/Candidate/MinIO/worker/Ollama/PostgreSQL, checks skills/employment/education on every result, verifies literal persisted evidence and browser presentation, and checks two fresh-key repeat analyses plus same-key replay. Captures are synthetic only in ignored `test-results/corpus`. `RUN_OLLAMA_CORPUS=1` is required; normal CI never makes external AI calls.

## Executed results

The original generation run took 13.5 minutes: **all ten application analyses succeeded (5/5 Turkish, 5/5 English), 120 grounded facts**. Skills, employment and education were present in every case. No failed/NoFindings model case was removed from this denominator.

| Synthetic PDF | Language | Original accepted facts | Application result |
| --- | --- | ---: | --- |
| Backend developer | Turkish | 6 | Succeeded |
| Frontend developer | Turkish | 11 | Succeeded |
| Data engineer | Turkish | 15 | Succeeded |
| DevOps engineer | Turkish | 11 | Succeeded |
| QA automation engineer | Turkish | 17 | Succeeded |
| Full stack engineer | English | 6 | Succeeded |
| Backend engineer | English | 15 | Succeeded |
| Cloud engineer | English | 13 | Succeeded |
| Data engineer | English | 10 | Succeeded |
| Quality engineer | English | 16 | Succeeded |

The **initial browser run was not green**: five Turkish cases failed an extra HTML-versus-extracted-text assertion despite successful model/DB results. HTML is not authoritative PDF text: layout and contact minimization change its representation. That comparison was replaced by immutable PDF hash checks and a separate Linux/PdfPig source-span verification. A subsequent run had one artifact-write failure because overlapping browser suites shared Playwright's output directory (9/10 passed); outputs were separated and captures moved outside Playwright cleanup. Neither failure justified weakening evidence checks or altering the model.

Final readback/UI verification: **10/10 passed in 18.3 seconds**. This final run reused acknowledged synthetic model results; it is **not** ten fresh inference calls. It reread the latest analysis (never cherry-picks an older success), downloaded its CV through authorized endpoints, verified SHA-256 identity, and displayed skills/employment/education in the browser. A network-disabled Linux SDK test then re-extracted all ten stored PDFs and verified every quote/value/UTF-16 span: **1 test passed**, with checks for **129 facts** in the latest records. The count differs because a fresh same-file Turkish repeat returned 15 facts rather than 6; both were grounded. Model recall/granularity is variable, not deterministic.

Fresh same-file Turkish and English repeat analyses succeeded, retained the immutable document/hash, and idempotent replay did not create an additional analysis. Repeat calls are separate from the ten distinct PDF samples.

The affected real PDF also passed the **normal authenticated API → worker → actual Ollama → PostgreSQL → browser** path: **Succeeded, 21 facts, all six categories, one attempt, 0 USD**. Its validated result is stored normally in the application; no raw model response or additional CV/browser recording was retained in diagnostics. Diagnostic files contain counts/status only. Historical failures remain untouched.

Other local verification: Release build 0 warnings/errors; full ordinary backend suite **150 passed** (140 existing + 10 new); adapter group **23 passed** (overlapping); dedicated fake-provider browser suite **4 passed**; npm ci/lint/build passed (existing bundle-size warning only); npm/NuGet audits reported zero vulnerabilities; Compose configuration/readiness and Candidate migration drift check passed. Corpus grounding is executed separately in Linux, not counted as a default-CI pass. Final CI/subsequent checks are recorded on [PR #25](https://github.com/atakanbektas/NexusHR/pull/25).

## Reproduce the corpus

With the local AI Compose stack healthy, Node 24 dependencies and Playwright Chromium installed:

```powershell
cd NexusHR.Web
$env:RUN_OLLAMA_CORPUS = '1'
npx playwright test ollama-corpus.spec.ts --workers=1 --output=../.tools/corpus-browser --reporter=list
```

This creates ten synthetic candidates/PDFs and makes twelve local calls, including two repeats. It consumes normal quotas; do not increase/reset quotas to make reruns green. To verify acknowledged records without ten extra calls, set `REUSE_OLLAMA_SYNTHETIC_CORPUS=1` and `OLLAMA_CORPUS_REPEATS=none`. Recovery requires exactly one matching synthetic candidate per case, checks the latest record and never hides failed outcomes. Use `tr`, `en` or `both` instead of `none` only when fresh repeats are desired. Normal CI skips real inference; these cases were executed locally.

Only synthetic PDF/results are saved in ignored `.tools/evidence-corpus`. Run the source test in Linux, matching deployment's newline/UTF-16 representation (from repository root after a Release build):

```powershell
$tests = Join-Path $PWD 'NexusHR.Candidate.IntegrationTests/bin/Release/net10.0'
$corpus = Join-Path $PWD '.tools/evidence-corpus'
docker run --rm --network none --mount "type=bind,source=$tests,target=/tests,readonly" --mount "type=bind,source=$corpus,target=/corpus,readonly" -e RUN_OLLAMA_CORPUS_VERIFY=1 -e OLLAMA_CORPUS_DIR=/corpus mcr.microsoft.com/dotnet/sdk:10.0 dotnet vstest /tests/NexusHR.Candidate.IntegrationTests.dll '--TestCaseFilter:FullyQualifiedName~OllamaCorpusGroundingTests'
```

Do not run browser suites concurrently with shared output/report directories. Never enable corpus capture for real CVs.

## Known limits and reanalysis

This branch still has **no OCR**. Scanned/image-only PDFs explicitly report the OCR limitation; embedded images are not interpreted. Text PDFs with readable text are supported. Model recall/completeness is not guaranteed; omitted unsupported facts are visible as a warning. Source matching proves textual support, not semantic truth or hiring suitability; human review remains required.

Existing failed records remain in history. After updating the local development containers, choose **CV Analizi → Analiz başlat** for a fresh analysis of the same stored PDF. There is no need to upload again or delete any database/volume. Bounded retry and daily quotas still apply. CPU model tests can take minutes per document.

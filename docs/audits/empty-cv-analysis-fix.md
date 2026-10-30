# Empty CV analysis — root cause and regression repair

Date: 2026-10-28. PR #24 branch `feature/docker-ollama-integration`; no main merge or database reset.

## Confirmed cause

The affected stored PDF was inspected locally without printing or saving its text. Only numeric metrics were emitted. Stored/downloaded length matched at **658 bytes**, and SHA-256 matched. PdfPig returned one page with **57 characters**, **52 non-whitespace characters**, and **zero image objects**. After the existing contact minimization, **zero readable/non-whitespace characters remained**. The extracted text was entirely accounted for by contact fields; professional information was not present in the extractable text layer.

This was not an OCR, storage corruption or demonstrated PdfPig text-loss defect. Contact minimization correctly protected the contact-only input and was not relaxed. There was no professional content to reconstruct, and no such content is invented by this repair.

The old worker did not validate minimized input before dispatch. The adapter rendered `PAGE 1` followed by whitespace. Ollama returned warnings including `No content provided for PAGE 1` and an empty facts array. These phrases came from the model response, not a production application string. Empty arrays satisfy the structural schema and have no invalid spans, so the evidence validator correctly rejected no individual facts; the worker incorrectly treated that structural validity as a useful successful analysis. The UI then displayed raw English warnings and a successful attempt.

## Repair

- Introduce testable input preparation using the **same** contact minimization rules. Preserve character positions and original page numbers. Exclude unreadable/minimized-empty pages; never send page headers without content.
- Log only page number, raw/minimized character/readable counts, image count, output fact/warning counts and token usage. No CV text, identity, prompt, quote or model warning body is logged.
- If no analyzable text remains, finish as `NoFindings` / `AI.NoAnalyzableText` **before inference dispatch**. No provider usage is charged; existing request quotas and attempt limits remain.
- If a provider returns zero supported facts for readable input, finish as `NoFindings` / `AI.NoSupportedFacts`, never `Succeeded`. Usage is still recorded/settled; no automatic repeated call or invented completion is introduced.
- A PDF with no readable text fails with `AI.NoReadableText` and an explicit Turkish OCR explanation. Image metadata is not serialized to a provider. Mixed readable/image documents retain a warning that only the text layer was analyzed.
- Preserve historical empty `Succeeded` records. Display them as **Eski boş analiz**, hide raw empty-response warnings and approval controls, and reject approval at the backend. Valid nonempty reviews keep the same role/version checks.
- `NoFindings` is a separate terminal outcome and uses the existing explicit retry path, original requester restriction, ETag, attempt cap and quota. It does not change Candidate verification, Hiring or Onboarding. Existing idempotency, cancellation, Outbox/Inbox and privacy boundaries remain.

No relational migration is required: status is already stored as a string. Deploy the matching API/frontend version together; older application versions do not know the additive `NoFindings` state. No historical result is rewritten.

## Verification

- Regression reproduces a real parsed contact-only synthetic PDF becoming empty after minimization; checks no provider dispatch and zero usage.
- A professional-source/empty-provider-response regression checks `NoFindings`, settled usage, idempotent replay, review rejection and guarded explicit retry.
- Tests cover exact offsets, blank first page/original page numbering, image-only PDF/OCR, real native payload content and rejection of whitespace-only payload before any HTTP request.
- Real Docker `qwen3:4b` runs validate both Turkish and English synthetic professional text through native JSON Schema output and unchanged evidence checks. No real CV is sent for these model tests.
- Browser-host scenarios cover contact-only input, empty professional response, OCR messaging, old empty-success presentation and normal evidence/review. Fake identity/provider remain confined to test assemblies; surrounding Candidate/Hiring chrome is synthetic in these browser-host checks.
- Normal business E2E separately verifies the actual Candidate → Hiring → Offer Acceptance → Onboarding flow with AI off.

Execution evidence: 47 targeted input/provider/reliability cases passed; complete ordinary backend suite 140 passed, opt-in native/browser cases deferred to dedicated runs; real native suite 4/4 passed; dedicated browser host 4/4 passed; unchanged business browser suite 3/3 passed; real synthetic Turkish PDF/Ollama upload/evidence/review/restart E2E passed in about 2.9 minutes. These overlapping runs are not added together as independent backend case counts. Release build, frontend lint/build, Compose/migration and final remote CI results are checked separately and reported on the PR.

The first push CI run exposed an existing Onboarding test startup race: it published to a fresh broker before requiring its receive endpoint to be ready. The parallel PR run passed that same test. The test now explicitly waits for MassTransit startup before publishing; assertions, broker-outage coverage and production transport settings are unchanged. The targeted real-broker test passed locally after this correction. This failed CI run is retained as evidence rather than hidden with an unexplained rerun.

## Retry the same stored CV

```powershell
git switch feature/docker-ollama-integration
git pull --ff-only
docker compose -f docker-compose.yml -f docker-compose.ai.yml --profile ai up -d --build --wait --wait-timeout 1200
```

Refresh the browser, open the same Candidate → **CV Analizi** and choose **Analiz başlat** to create a fresh analysis. The old record remains as audit history. The affected contact-only file now produces a Turkish **Doğrulanmış sonuç yok** explanation without inference, not a fabricated professional profile. A full professional CV is needed for professional findings. The existing one-CV-per-candidate upload rule is unchanged; replacing a stored CV is a separate workflow, so use a new candidate for a different full PDF until controlled replacement/versioning is implemented.

## OCR is a separate development

OCR is not installed or silently invoked. A separate opt-in local OCR stage could rasterize bounded pages in an isolated container, extract Turkish/English text under CPU/RAM/page/pixel/time limits, record extractor/source provenance and preserve page evidence for human review. It must include scanned and mixed-document regression fixtures and must not silently replace the existing PdfPig/text workflow or send documents to a cloud provider. It is not required to fix this contact-only document and is not claimed as implemented.

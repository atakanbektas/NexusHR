# Local document extraction and review

The delivered pipeline uses PdfPig, optional local Tesseract OCR and the existing **qwen3:4b text model**. No image/vision inference or model change is introduced. AI results never gate Candidate verification, Hiring or Onboarding.

## Startup

```sh
docker compose -f docker-compose.yml -f docker-compose.ai.yml -f docker-compose.multimodal.yml -f docker-compose.security.yml --profile ai --profile security config --quiet
docker compose -f docker-compose.yml -f docker-compose.ai.yml -f docker-compose.multimodal.yml -f docker-compose.security.yml --profile ai --profile security up -d --build --wait --wait-timeout 1200
```

Initial model/signature downloads need internet; 16 GiB Docker VM memory is recommended for the combined stack. Inference, OCR and scanning use private networks. OCR/scanner have no published ports. Downloaders are separate from document processing. Existing named volumes remain; never use `down -v` to fix failures. See [operations](operations.md).

## Authoritative extraction

- Files are bounded to 5 MiB; signatures, MIME types and decodability are checked. PdfPig remains first. Sparse pages or pages substantially covered by images use local OCR when enabled. Readable original pages retain their text and numbering.
- Tesseract `tur+eng`, Poppler and Pillow run under non-root, read-only filesystem, bounded tmpfs, native CPU/memory/time limits and one active request. Cancellation kills the native process group and cleans temporary files.
- JPEG/PNG/WEBP and scanned/mixed PDF are supported. OCR is bounded to eight pages; native text PDF extraction to 50. Unsupported/oversized documents fail explicitly, never silently truncate.
- Every page has one authoritative coordinate system. Accepted facts retain OCR provenance/word boxes. Pixels and box metadata are not sent to the provider. Contact minimization preserves offsets but is **not anonymization**.
- Blank/illegible OCR fails clearly. Professional text without accepted evidence produces `NoFindings`. Invalid provider envelopes fail; invalid individual facts are discarded without inventing replacements.

## Versions and review

Original POST upload preserves duplicate-CV behavior. `PUT /api/candidates/{id}/documents/cv` replaces a pre-verification CV only with `If-Match: "<current-document-id>"`. Concurrent writers have one winner. Verified snapshots are locked. Old document bytes, hashes, analysis references and review records remain available.

CV-reader roles protect `GET .../documents/cv/versions` and `GET .../documents/cv/versions/{documentId}`, including preview/download. Candidate-list access does not grant CV access; Admin/DepartmentManager have no implicit override. Durable upload intents, compensation and reconciliation remain in place.

Model quotes resolve uniquely on the declared page; the backend owns offsets. Structured fields require literal evidence **within their parent fact quote**. Employment, education, skills and language tables show validated values. Calendar-month coverage uses explicit ISO ranges and merges overlaps; open-ended/ambiguous dates are never estimated.

Per-fact accept/reject/correct actions append separate HR records under optimistic concurrency. Original model output is immutable. Historical analyses preview their own CV version. Human corrections are labelled separately and never represented as AI-verified facts.

## Verification and limits

Normal CI uses a deterministic provider, real PostgreSQL/RabbitMQ/MinIO, browser review/version regressions, actual OCR and private ClamAV. No paid API calls or personal CV fixtures. Native Qwen tests require `RUN_OLLAMA_TESTS=1`; the full scan-to-OCR-to-messaging-to-Qwen-to-persistence test uses `RUN_OCR_OLLAMA_TESTS=1` and a loopback test endpoint. See [results](audits/portfolio-hardening.md).

OCR can misread glyphs/layouts. Evidence validates extracted text alignment, not the truth of a CV claim. HR must compare the source preview. Complete extraction, automated hiring decisions, face analysis and vision inference are not claimed.

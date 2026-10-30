# ADR 006: Immutable CV versions, local OCR and separate human decisions

Status: Accepted for the portfolio integration branch, 2026-10-30.

## Context

Replacing bytes beneath a document ID invalidates historical evidence. Scanned documents need bounded local extraction. Asking models to infer offsets or invent missing fields weakens evidence. Human corrections must remain distinguishable from source facts.

## Decision

Retain immutable documents with increasing revisions and one current version per candidate/type. Use a PostgreSQL partial unique index, aggregate xmin, conditional HTTP replacement and the existing durable storage coordinator. Replacement stops at verification. Old analyses reference their exact document.

Keep PdfPig first and isolate Tesseract OCR for sparse/scanned pages and images. Each page has one authoritative text representation. Source boxes are backend metadata; raw image inference is excluded. Keep qwen3:4b and unique exact/whitespace-only quote resolution. Child fields must be grounded within the parent quote.

Append HR fact decisions/corrections without modifying stored model results. Calculate only explicit calendar date coverage. Enable fail-closed scanning independently of AI; Candidate Production startup requires it.

## Consequences

Retention includes old versions and review history; erasure requires an approved policy across metadata, objects and backups. OCR adds CPU/memory-limited dependencies. Native scanning cannot certify arbitrary files safe. Invalid individual facts cannot invalidate valid siblings; zero grounded facts never becomes useful success. Recovery must include all document versions and messaging state. Scoped restore tests are not whole-platform production certification.

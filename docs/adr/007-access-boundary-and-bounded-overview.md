# ADR 007: Explicit single-organization access and bounded overview composition

Status: Accepted, 2026-11-02.

## Business boundary

This installation represents one employer. Candidate has no department ownership before Hiring; Hiring/Onboarding store a free-text department snapshot, not an authoritative membership key. Existing role readers have organization-wide visibility. DepartmentManager currently reads Candidate contacts, Hiring and Onboarding across departments. It does **not** read CVs/analysis, edit candidates, send offers or complete onboarding. Admin has no implicit override. HR/Auditor CV access is intentionally organization-wide; analysis mutations additionally require the requesting subject. Anonymous offer responses are scoped by expiring single-use hashed capabilities.

Resource identity remains authoritative: CV version and analysis lookups require both candidate ID and resource ID; task mutations resolve task IDs within the selected onboarding. HTTP regression tests exercise ID substitution. A valid role is not an ownership boundary by itself.

Do not label this multi-tenant or department-isolated. Deploy only where the employer approves this access matrix. A department-restricted deployment needs stable department identifiers, signed authoritative membership claims, candidate assignment rules before Hiring, query-time filtering in **all** services and Gateway, backfill and cross-department regression tests. Guessing ownership from free text or candidate creator would silently remove legitimate access and create inconsistencies. That separate business/schema change is not fabricated here.

## Overview composition

Unfiltered Candidate overview delegates pagination/count to Candidate and enriches at most one page (100 IDs) from Hiring. It no longer downloads the whole organization for every page. Same-time creation ordering is stable on CreatedAtUtc + Id, backed by a descending index.

Display-status filters retain existing semantics: Candidate statuses only apply where no active Hiring status overrides the display. This requires a cross-service join. Until a durable projection with a backfill/reconciliation design is justified, filtered composition has a 5000-candidate work budget and 20-second request deadline. Over-budget requests fail explicitly with compatible ProblemDetails code/errors and a request to narrow search, never silently truncate or report a fabricated count. Page offsets use long arithmetic.

Downstream error bodies are not retained in exceptions. HTTP failures preserve authorization status and expose safe ProblemDetails. Cancellation propagates to downstream HTTP. No exactly-once or snapshot-consistent cross-database listing is claimed.

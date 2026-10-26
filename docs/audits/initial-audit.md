# Initial technical audit

Date: 2026-10-26. Scope: Phase 0 followed only by the scoped Phase 1 authorization repair. Baseline working tree was clean on branch1; work proceeds on `codex/audit-candidate-upload-authorization`.

## Inspection scope and evidence

Reviewed the solution/project graph, all backend layers and request handlers, validators/contracts, shared security, EF mappings and migration history/snapshot, tests, React routes/auth/API clients and candidate workflows, frontend compiler/lint/build settings and lockfile, Dockerfiles/ignore files, Compose, Keycloak realm import, application settings, and existing README. No AGENTS.md or GitHub Actions workflow existed in the baseline. This is a source audit, not proof of a successful deployment.

Implemented: Candidate CRUD subset (create/read/update), pagination/search/status filtering, heuristic PDF extraction, single CV per candidate, CV download, document verification/status progression, Keycloak role mapping, YARP routing, Docker orchestration, and eight domain unit tests. The integration project initially contained one empty passing test. Hiring, messaging reliability, frontend tests, and CI are planned rather than implemented.

## Confirmed defects and gaps

| Priority | Finding and source evidence | Consequence / next action |
| --- | --- | --- |
| P0 | `CandidatesController.UploadCv` has no action role restriction; controller only has `[Authorize]`. Frontend creation requires HrSpecialist. | Any authenticated role can attempt CV mutation directly. First Phase 1 repair: require CandidateEditors and test HTTP authorization. |
| P1 | `UploadCandidateCvCommandHandler` commits metadata through AddAsync before MarkDocumentsPending and UpdateAsync. | A rejected status transition or second save failure leaves metadata/object without the intended status. Introduce one DB commit and failure-aware storage coordination. |
| P1 | Upload compensates only metadata-save failure; DeleteAsync can fail and replace the original error; no reconciliation exists. | Objects may be orphaned after failures/crashes. Add bounded compensation and durable reconciliation; handle uncertain DB outcomes before deleting. |
| P1 | Create/update use email prechecks before save; unique indexes exist but exceptions are not translated. Document precheck has the same race. No EF concurrency token is configured; detached updates write whole entities. | Concurrent requests can yield unhandled errors or lost state. Test actual PostgreSQL races and return deterministic conflicts. |
| P1 | Compose, base appsettings, and realm import include known development passwords; HTTP metadata/storage, start-dev, and Development Docker defaults are enabled. | Deployment defaults are unsafe for production. Separate production configuration and inject secrets; do not copy demo users to production. No evidence these are live production secrets. |
| P1 | Controller errors are ad hoc code/errors/message objects, model binding uses framework errors, and no exception handler is configured. | Inconsistent client contract; define ProblemDetails while maintaining frontend compatibility. |
| P1 | Integration test is empty; no handler/storage/API/frontend/E2E tests exist at baseline. | Reliability and security boundaries lack automated evidence. Add focused tests with each repair. |
| P2 | API runs MigrateAsync unconditionally at startup; API/storage/Keycloak Compose dependencies mostly use service_started. | Startup depends on infrastructure timing and migration privileges. Add readiness and explicit migration deployment strategy. |
| P2 | Validators inspect extension/MIME/signature and 5 MiB size, with a 6 MiB HTTP cap; extraction processes every PDF page without page/text budgets. Several Must predicates dereference potentially null inputs. | Bound parser resources and test malformed inputs, nulls, lengths and cancellation. A PDF signature alone does not validate a full PDF. |
| P2 | Frontend saves candidate then uploads; upload failure loses the created ID from the retry workflow. No edit or existing-draft upload UI exists. | Retrying can hit duplicate email and leave an incomplete draft. Preserve ID and support recoverable upload. |
| P2 | No Candidate health endpoints, trace/metric export, correlation setup, CI, security scanning, or backend dependency lock/SDK pin. MinIO/nginx use mutable tags. | Reproducibility and operational verification need dedicated milestones. |
| P3 | README claims event-driven onboarding although messaging and Hiring are absent. | Replace with an accurate portfolio narrative after validated milestones. |

## Potential risks requiring verification

- JWT audience validation is disabled; azp is checked instead. Confirm intended resource audience and Keycloak mapper configuration before changing the security model.
- Claims transformation parses role JSON without handling malformed JSON or non-string array elements. Test robustness against correctly signed unexpected claims.
- Multiple service instances running migrations, MinIO bucket creation races, Keycloak discovery/JWKS addresses from inside Compose, HTTP/HTTPS redirect behavior behind YARP, and concurrent upload/verify behavior require real infrastructure tests.
- Listing computes `(page - 1) * pageSize` using int and has no page upper bound: extreme page values risk overflow. Filtering/counting are separate queries and are not snapshot-consistent.
- No vulnerability status, successful package restore, compilation result, or working deployment is established by this audit.

## Dependency/configuration review

All .NET projects target net10.0. EF Core, EF Design, OpenAPI and JWT packages use 10.0.10; Npgsql provider is 10.0.3 (same major, compatibility still requires restore/build). Application uses FluentValidation 12.1.1 and MediatR 14.2.0; Infrastructure uses Minio 7.0.0 and PdfPig 0.1.15; Gateway uses YARP 2.3.0. Tests use xUnit 2.9.3, VS runner 3.1.5, test SDK 18.8.1 and coverlet 10.0.1. Do not assume these are supported, vulnerable, or incompatible solely from their version numbers.

Frontend package.json and package-lock.json root declarations agree; all declared direct dependencies have lock entries. React 19, MUI 9, TypeScript 6, Vite 8 and ESLint 10 are declared. Docker selects Node 24, which satisfies the checked lockfile engine ranges for Vite, ESLint and typescript-eslint. Registry availability, transitive peer compatibility, image availability and advisories remain unverified until restore/install/build/scanning can run. No dependencies were upgraded during this audit. The authorization tests add Microsoft.AspNetCore.TestHost 10.0.10 to match the existing ASP.NET version.

## Check execution (initial and final)

| Command/check | Result |
| --- | --- |
| `dotnet --info`, `dotnet build NexusHR.slnx`, `dotnet test NexusHR.slnx` | Could not start: dotnet executable unavailable on PATH or usual installation path. |
| `npm --prefix NexusHR.Web ci`, `npm --prefix NexusHR.Web run build`, `npm --prefix NexusHR.Web run lint` | Could not start: npm unavailable. A bundled Node executable exists, but no npm CLI was found in checked runtime locations. |
| `docker version`, `docker compose config --quiet` | Could not start: Docker unavailable. Container build, PostgreSQL/MinIO tests and browser E2E are unverified. |
| Git baseline/status/branch | Git found in GitHub Desktop bundle; clean baseline confirmed; dedicated branch created after sandbox approval. |
| Final `git diff --check` | Passed for tracked changes. |
| Final PowerShell static checks | Passed: eight project XML files parse and project references resolve; direct frontend dependency declarations match lock entries; deployment/API/Gateway JSON parses; upload editor attribute and all four required documents exist. These are source checks, not build or runtime tests. |

No frontend test script is defined. After provisioning .NET 10, Node 24/npm, and Docker, run the commands above, then `docker compose build` and infrastructure smoke tests. Also run `dotnet list NexusHR.slnx package --vulnerable --include-transitive` and `npm --prefix NexusHR.Web audit`. Do not interpret unavailable checks as passes.

## Milestone delivery record

Phase 0 files: `AGENTS.md`, `docs/architecture/overview.md`, `docs/audits/initial-audit.md`, `docs/roadmap.md`. Scope and acceptance criteria are recorded without claiming runtime certification.

Scoped Phase 1 files: `NexusHR.Candidate.Api/Controllers/CandidatesController.cs` adds the existing editor-role requirement to upload; `NexusHR.Candidate.IntegrationTests/NexusHR.Candidate.IntegrationTests.csproj` adds TestHost; `NexusHR.Candidate.IntegrationTests/UnitTest1.cs` is removed and replaced by `NexusHR.Candidate.IntegrationTests/Candidates/CvUploadAuthorizationTests.cs`. No routes, successful response contracts, role definitions, schema, or frontend files change. Twelve test cases cover anonymous 401, forbidden non-editor/roleless callers, editor access to file validation, and combined editor/reader roles. The test host uses real MVC/authorization and a test-only identity provider, not Keycloak, PostgreSQL, MinIO, or production startup. Execution remains blocked by the missing SDK. Full frontend workflow, JWT validation and infrastructure correctness still require verification. Final static results are recorded in the check table above.

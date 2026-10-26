# NexusHR contributor instructions

- Preserve existing Candidate Clean Architecture boundaries and working routes/contracts. Domain has no infrastructure dependencies; Application depends on abstractions; Infrastructure implements them; API hosts and maps HTTP.
- Work on a dedicated `codex/` feature branch. Keep changes focused. Never push directly to main or automatically merge pull requests. Do not commit credentials or describe development fixtures as production-safe.
- The current authorized scope is integration and completion of Candidate, Hiring, and Onboarding. Preserve useful implementation from existing branches, work in reviewable milestones, and update documentation to reflect actual behavior.
- Backend authorization is authoritative. Preserve distinct candidate-reader, CV-reader and editor permissions; Admin has no implicit override. Frontend guards only control presentation.
- PostgreSQL and MinIO do not share an atomic transaction. Plan compensation, retry and durable reconciliation explicitly. Test uniqueness races and concurrency against PostgreSQL rather than trusting prechecks or in-memory providers.
- Add meaningful regression tests for defects. Never hide failing checks or count empty tests as integration coverage. Keep test authentication implementations confined to test assemblies.
- Use .NET 10 and Node 24/npm. Run `dotnet build NexusHR.slnx`, `dotnet test NexusHR.slnx`, then in NexusHR.Web run `npm ci`, `npm run lint`, `npm run build` and any relevant tests. Run `docker compose config --quiet` and container/infrastructure checks when applicable. Missing tooling is a reported blocker, never a pass.
- For each milestone report changed files, tests executed, results and remaining risks. Consult `docs/architecture/overview.md`, `docs/audits/initial-audit.md` and `docs/roadmap.md` before changing cross-cutting behavior.

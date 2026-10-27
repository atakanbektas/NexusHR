# ADR 001: CV commit with durable object compensation

Status: implemented; see integration verification for executed tests.

The original workflow saved metadata before changing candidate state. A failed transition could leave inconsistent metadata and an orphaned object.

We keep PostgreSQL and MinIO independent. Validate state before I/O, persist an upload intent, upload a uniquely named object under a two-minute deadline, then lock the intent and commit metadata/status/intent deletion in one PostgreSQL transaction. `xmin` and uniqueness constraints resolve concurrent requests.

On failure, a fresh context locks the intent and checks persistent references before deletion. Missing intent means finalization or another cleanup already resolved it. Failed cleanup retains the intent. A worker retries expired intents after 15 minutes, using the same row lock, every minute in batches of 100. A reconciled reservation cannot subsequently finalize.

Trade-offs: an extra write, transient orphan objects while storage is unavailable, and recovery processing. Lost success acknowledgements can still yield HTTP errors after a valid commit; clients inspect/reuse the candidate. We do not claim cross-resource atomicity or automatically delete pre-existing orphan objects from older versions.

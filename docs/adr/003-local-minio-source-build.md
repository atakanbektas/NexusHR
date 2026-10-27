# ADR 003: Source-built local MinIO

Status: implemented for local demos and tests.

Public Docker Hub images are unavailable and Quay denies anonymous historical pulls. Build `nexushr-minio:2025-10-15` using `deploy/minio/Dockerfile`, from official `RELEASE.2025-10-15T17-29-55Z`. This preserves the S3 SDK, API and existing volumes. Upstream's AGPL license is included in the image.

Sources: [official release/build instructions](https://github.com/minio/minio/releases/tag/RELEASE.2025-10-15T17-29-55Z), [archived upstream](https://github.com/minio/minio).

The slower first build solves reproducibility, not ongoing security maintenance. Production must select a maintained S3 service/distribution, assess licensing and validate compatibility before migrating data. We do not silently replace storage or reset volumes.

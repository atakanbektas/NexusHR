# Upstream removed public images. Build the official, pinned source release.
# AGPL-3.0 source: https://github.com/minio/minio/tree/RELEASE.2025-10-15T17-29-55Z
FROM golang:1.26-bookworm AS build
ARG MINIO_VERSION=RELEASE.2025-10-15T17-29-55Z
RUN git clone --depth 1 --branch ${MINIO_VERSION} https://github.com/minio/minio.git /src
WORKDIR /src
RUN CGO_ENABLED=0 go build -trimpath -o /out/minio .

FROM debian:bookworm-slim
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates curl && rm -rf /var/lib/apt/lists/*
COPY --from=build /out/minio /usr/local/bin/minio
COPY --from=build /src/LICENSE /licenses/minio-LICENSE
EXPOSE 9000 9001
HEALTHCHECK --interval=10s --timeout=5s --start-period=20s CMD curl --fail --silent http://localhost:9000/minio/health/ready || exit 1
ENTRYPOINT ["minio"]

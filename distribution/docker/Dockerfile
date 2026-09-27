# Build from the repository root:
# docker build -f distribution/docker/Dockerfile -t komarr:local .
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS backend

ARG TARGETARCH
ARG TARGETVARIANT
ARG KOMARR_VERSION=0.1.0.0
WORKDIR /src
COPY .editorconfig ./
COPY Logo/ ./Logo/
COPY src/ ./src/
RUN set -eu; \
    case "${TARGETARCH:-amd64}${TARGETVARIANT:-}" in \
        amd64) rid=linux-musl-x64 ;; \
        arm64|arm64v8) rid=linux-musl-arm64 ;; \
        arm|armv7|armv6) rid=linux-musl-arm ;; \
        *) echo "Unsupported architecture: ${TARGETARCH:-unknown}${TARGETVARIANT:-}" >&2; exit 1 ;; \
    esac; \
    dotnet restore src/Readarr.sln -p:Configuration=Release -p:Platform=Posix -r "$rid"; \
    dotnet publish src/NzbDrone.Console/Readarr.Console.csproj \
        -c Release -f net10.0 -r "$rid" --no-restore --self-contained false \
        -p:Platform=Posix -p:AssemblyVersion="$KOMARR_VERSION" \
        -o /publish; \
    dotnet publish src/NzbDrone.Mono/Readarr.Mono.csproj \
        -c Release -f net10.0 -r "$rid" --no-restore --self-contained false \
        -p:Platform=Posix -p:AssemblyVersion="$KOMARR_VERSION" \
        -o /publish

FROM --platform=$BUILDPLATFORM node:24-alpine AS frontend
WORKDIR /src
COPY package.json yarn.lock ./
RUN corepack yarn install --frozen-lockfile --network-timeout 120000
COPY frontend/ ./frontend/
COPY tsconfig.json ./
RUN corepack yarn build --env production

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
LABEL org.opencontainers.image.title="Komarr" \
      org.opencontainers.image.description="Self-hosted manga acquisition and collection manager" \
      org.opencontainers.image.source="https://github.com/GodGMN/Komarr" \
      org.opencontainers.image.licenses="GPL-3.0-only"
RUN apk add --no-cache sqlite-libs icu-libs ca-certificates curl
ENV DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
WORKDIR /app
COPY --from=backend /publish/ ./
COPY --from=frontend /src/_output/UI/ ./UI/
VOLUME ["/config", "/manga", "/downloads"]
EXPOSE 8787
HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
    CMD curl -fsS http://127.0.0.1:8787/ping || exit 1
ENTRYPOINT ["./Komarr"]
CMD ["-nobrowser", "-data=/config"]

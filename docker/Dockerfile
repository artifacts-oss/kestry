# Build from the repository root: docker build -f docker/Dockerfile -t kestry .

# --- Frontend ---------------------------------------------------------------
# NODE_IMAGE can point at a mirror when Docker Hub rate-limits the build.
ARG NODE_IMAGE=node:22-slim
FROM --platform=$BUILDPLATFORM ${NODE_IMAGE} AS web
WORKDIR /src/src/kestry-web
COPY src/kestry-web/package.json src/kestry-web/package-lock.json ./
RUN npm ci
COPY src/kestry-web ./
# Vite writes to ../Kestry.Server/wwwroot
RUN npm run build

# --- Server -----------------------------------------------------------------
# Runs on the build platform and cross-publishes with -r, so arm64 images are not built under emulation.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src
COPY src/Kestry.Server/Kestry.Server.csproj src/Kestry.Server/
# Docker's TARGETARCH (amd64/arm64) -> .NET runtime identifier
RUN case "$TARGETARCH" in amd64) echo linux-x64 > /rid;; arm64) echo linux-arm64 > /rid;; *) echo "unsupported arch $TARGETARCH" >&2; exit 1;; esac \
    && dotnet restore src/Kestry.Server -r "$(cat /rid)"
COPY src/Kestry.Server src/Kestry.Server
COPY --from=web /src/src/Kestry.Server/wwwroot src/Kestry.Server/wwwroot
RUN dotnet publish src/Kestry.Server -c Release -r "$(cat /rid)" --no-self-contained --no-restore -o /app

# --- Runtime ----------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update \
    && apt-get install -y --no-install-recommends aria2 \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /data /downloads \
    && chown app:app /data /downloads
WORKDIR /app
COPY --from=build /app .
# "Urls" (not ASPNETCORE_URLS) because appsettings.json sets it and would win otherwise.
ENV Urls=http://+:8080 \
    Kestry__DataDir=/data \
    Kestry__AllowedRoots__0=/downloads
VOLUME ["/data", "/downloads"]
EXPOSE 8080 6881/tcp 6881/udp
USER app
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s CMD ["dotnet", "Kestry.Server.dll", "--healthcheck"]
ENTRYPOINT ["dotnet", "Kestry.Server.dll"]

# Build image
# Runs on the builder's native arch and cross-compiles: the .NET SDK crashes under QEMU
# (AccessViolationException) when building a foreign-arch image via emulation.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build

ARG TARGETARCH

WORKDIR /src

COPY . .

# Cache mount keeps downloaded packages across retries/builds; timeout fails a stalled restore fast.
RUN --mount=type=cache,target=/root/.nuget/packages \
    ARCH=$([ "$TARGETARCH" = "arm64" ] && echo arm64 || echo x64) && \
    timeout 600 dotnet restore RecordService/RecordService.csproj -a $ARCH --disable-parallel

RUN --mount=type=cache,target=/root/.nuget/packages \
    ARCH=$([ "$TARGETARCH" = "arm64" ] && echo arm64 || echo x64) && \
    dotnet publish RecordService/RecordService.csproj \
    -c Release \
    -a $ARCH \
    --no-restore \
    -o /publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0

WORKDIR /app

COPY --from=build /publish .

# Chromium is only needed for the PEDro scraper, which is off by default; production builds
# pass INSTALL_PLAYWRIGHT=false to skip it (smaller image, faster arm64 build).
ARG INSTALL_PLAYWRIGHT=true
RUN if [ "$INSTALL_PLAYWRIGHT" = "true" ]; then \
      dotnet exec --runtimeconfig /app/RecordService.runtimeconfig.json --depsfile /app/RecordService.deps.json /app/Microsoft.Playwright.dll install --with-deps chromium; \
    fi

ENTRYPOINT ["dotnet", "RecordService.dll"]
# Build image
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY . .

RUN dotnet restore RecordService/RecordService.csproj

RUN dotnet publish RecordService/RecordService.csproj \
    -c Release \
    -o /publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0

WORKDIR /app

COPY --from=build /publish .

RUN dotnet exec --runtimeconfig /app/RecordService.runtimeconfig.json --depsfile /app/RecordService.deps.json /app/Microsoft.Playwright.dll install --with-deps chromium

ENTRYPOINT ["dotnet", "RecordService.dll"]
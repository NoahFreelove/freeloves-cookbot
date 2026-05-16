# syntax=docker/dockerfile:1.7
# Phase 9 / Plan 09-06 / PROD-01 / PROD-03 / D-43 — multi-stage build for FreelovesCookBot.
# Build stage uses .NET 10 SDK; runtime stage uses ASP.NET 10 base (Debian; includes curl
# by default, used by docker-compose.yml healthcheck stanza).

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first to maximize layer cache hits on dotnet restore.
COPY *.sln ./
COPY src/CookBot.Domain/*.csproj src/CookBot.Domain/
COPY src/CookBot.Application/*.csproj src/CookBot.Application/
COPY src/CookBot.Infrastructure/*.csproj src/CookBot.Infrastructure/
COPY src/CookBot.Web/*.csproj src/CookBot.Web/
COPY tests/CookBot.Tests/*.csproj tests/CookBot.Tests/
RUN dotnet restore src/CookBot.Web/CookBot.Web.csproj

# Copy remaining source and publish.
COPY src/ src/
RUN dotnet publish src/CookBot.Web/CookBot.Web.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Phase 9 / Plan 09-06 — install curl for docker-compose.yml healthcheck stanza.
# The mcr.microsoft.com/dotnet/aspnet:10.0 base image is minimal and does NOT include
# curl by default (verified empirically; 09-RESEARCH line 97/548 claimed otherwise — Rule 1 fix).
# Without this, the compose healthcheck reports "unhealthy" forever even though /healthz
# itself returns 200. apt-get adds ~3-4 MB; acceptable for clear D-43 semantics.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app ./

# PROD-03 + PITFALL M4: bind on 0.0.0.0; default localhost binding is unreachable from outside the container.
ENV ASPNETCORE_URLS=http://+:7000
# PITFALL M7: stable UTC inside the container — guarantees AiUsageLog Timestamp + DataProtection
# key rotation use a known clock regardless of host TZ.
ENV TZ=UTC
# Default port; compose can override via COOKBOT_PORT mapping.
EXPOSE 7000
ENTRYPOINT ["dotnet", "CookBot.Web.dll"]

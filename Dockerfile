# syntax=docker/dockerfile:1.7
# Multi-stage build for FreelovesCookBot.
# Build stage uses the .NET 10 SDK; the runtime stage uses the smaller ASP.NET 10 base.

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

# The ASP.NET base image doesn't ship curl, which the docker-compose.yml healthcheck
# uses. Without it the healthcheck reports "unhealthy" forever even though /healthz
# returns 200. Adds ~3-4 MB.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app ./

# Bind on 0.0.0.0 — the default localhost binding is unreachable from outside the container.
ENV ASPNETCORE_URLS=http://+:7000
# Pin the container clock to UTC so timestamps and key rotation stay consistent
# regardless of the host's timezone.
ENV TZ=UTC
# Default port; compose can override via COOKBOT_PORT mapping.
EXPOSE 7000
ENTRYPOINT ["dotnet", "CookBot.Web.dll"]

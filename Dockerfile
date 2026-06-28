# ── Stage 1: Build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Restore dependencies first (layer cache optimization)
COPY src/JobFinder.Api/JobFinder.Api.csproj ./src/JobFinder.Api/
RUN dotnet restore ./src/JobFinder.Api/JobFinder.Api.csproj

# Copy the rest of the source and build
COPY src/ ./src/
RUN dotnet publish ./src/JobFinder.Api/JobFinder.Api.csproj \
    --configuration Release \
    --output /app/publish

# ── Stage 2: Runtime ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copy built artifacts from the build stage
COPY --from=build /app/publish .

# Set the environment to production by default for the Docker image
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 5002

ENTRYPOINT ["dotnet", "JobFinder.Api.dll"]

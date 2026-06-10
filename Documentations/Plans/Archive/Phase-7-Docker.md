# Phase 7 — Docker

Dockerfiles for each deployable service and a single `docker-compose.yml` that wires everything together.

---

## Pre-checklist

- [ ] All backend projects added to `Roaster.slnx`
- [ ] `Roaster.slnx` is at the repo root (same level as `docker-compose.yml`)

---

## Dockerfile Strategy

Each Dockerfile copies the full `src/` directory. No need to enumerate individual `.csproj` files — `dotnet restore` resolves all project references from `Roaster.slnx`.

```
docker/
  Dockerfile.api
  Dockerfile.consumer
  Dockerfile.migration
docker-compose.yml
```

---

## docker/Dockerfile.api

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Roaster.slnx ./
COPY src/ src/
RUN dotnet restore src/Roaster.Api/Roaster.Api.csproj
RUN dotnet publish src/Roaster.Api/Roaster.Api.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Roaster.Api.dll"]
```

---

## docker/Dockerfile.consumer

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Roaster.slnx ./
COPY src/ src/
RUN dotnet restore src/Roaster.Consumer/Roaster.Consumer.csproj
RUN dotnet publish src/Roaster.Consumer/Roaster.Consumer.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Roaster.Consumer.dll"]
```

No `EXPOSE` — background worker, no inbound HTTP.

---

## docker/Dockerfile.migration

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Roaster.slnx ./
COPY src/ src/
RUN dotnet restore src/Roaster.Migration/Roaster.Migration.csproj
RUN dotnet publish src/Roaster.Migration/Roaster.Migration.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Roaster.Migration.dll"]
```

Uses `dotnet/runtime` (not `aspnet`) — no web server needed.

---

## docker-compose.yml

```yaml
name: maverices

services:

  postgres:
    image: postgres:17-alpine
    environment:
      POSTGRES_DB:       maverices
      POSTGRES_USER:     maverices
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:-changeme}
    volumes:
      - postgres_data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U maverices -d maverices"]
      interval: 10s
      timeout: 5s
      retries: 5
    restart: unless-stopped

  rabbitmq:
    image: rabbitmq:4-management-alpine
    environment:
      RABBITMQ_DEFAULT_USER: guest
      RABBITMQ_DEFAULT_PASS: ${RABBITMQ_PASSWORD:-guest}
    ports:
      - "5672:5672"
      - "15672:15672"   # Management UI → http://localhost:15672
    healthcheck:
      test: ["CMD", "rabbitmq-diagnostics", "-q", "ping"]
      interval: 15s
      timeout: 10s
      retries: 5
    restart: unless-stopped

  ollama:
    image: ollama/ollama:latest
    volumes:
      - ollama_models:/root/.ollama
    ports:
      - "11434:11434"
    # GPU passthrough — remove the deploy block if no NVIDIA GPU
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              count: 1
              capabilities: [gpu]
    healthcheck:
      test: ["CMD-SHELL", "curl -sf http://localhost:11434/api/tags || exit 1"]
      interval: 30s
      timeout: 10s
      retries: 5
    restart: unless-stopped

  migration:
    build:
      context: .
      dockerfile: docker/Dockerfile.migration
    environment:
      MIGRATION_CONNECTION_STRING: >-
        Host=postgres;Database=maverices;Username=maverices;Password=${POSTGRES_PASSWORD:-changeme}
    depends_on:
      postgres:
        condition: service_healthy
    restart: "no"

  maverices-api:
    build:
      context: .
      dockerfile: docker/Dockerfile.api
    ports:
      - "8080:8080"
    environment:
      ConnectionStrings__Postgres: >-
        Host=postgres;Database=maverices;Username=maverices;Password=${POSTGRES_PASSWORD:-changeme}
      RabbitMQ__Uri:    "amqp://guest:${RABBITMQ_PASSWORD:-guest}@rabbitmq:5672"
      Cors__Origins__0: "http://localhost:5094"
      Cors__Origins__1: "https://localhost:7052"
    depends_on:
      postgres:  { condition: service_healthy }
      rabbitmq:  { condition: service_healthy }
      migration: { condition: service_completed_successfully }
    healthcheck:
      test: ["CMD-SHELL", "curl -sf http://localhost:8080/health || exit 1"]
      interval: 15s
      timeout: 5s
      retries: 5
    restart: unless-stopped

  maverices-consumer:
    build:
      context: .
      dockerfile: docker/Dockerfile.consumer
    environment:
      ConnectionStrings__Postgres: >-
        Host=postgres;Database=maverices;Username=maverices;Password=${POSTGRES_PASSWORD:-changeme}
      RabbitMQ__Uri:    "amqp://guest:${RABBITMQ_PASSWORD:-guest}@rabbitmq:5672"
      Ollama__Endpoint: "http://ollama:11434"
      Ollama__ModelId:  "deepseek-coder:6.7b"
    depends_on:
      postgres:  { condition: service_healthy }
      rabbitmq:  { condition: service_healthy }
      ollama:    { condition: service_healthy }
      migration: { condition: service_completed_successfully }
    restart: unless-stopped

volumes:
  postgres_data:
  ollama_models:
```

---

## First-time Setup

After `docker compose up`, pull the AI model:

```bash
docker compose exec ollama ollama pull deepseek-coder:6.7b
```

The `ollama_models` volume persists model weights across restarts.

---

## Service Startup Order

```
postgres ──► migration ──► maverices-api
                       └──► maverices-consumer
rabbitmq ──────────────────► maverices-api
                        └──► maverices-consumer
ollama  ───────────────────► maverices-consumer
```
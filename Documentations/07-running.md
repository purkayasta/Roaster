# 7. Running the Project

## Prerequisites

- .NET 10 SDK
- Docker Desktop
- [Ollama](https://ollama.com/) installed **natively** on the host (Docker can't reach Apple
  Silicon/GPU acceleration)

## Step 1 — Start Ollama and pull the model

```bash
ollama serve                        # or run as a background service
ollama pull qwen2.5-coder:3b        # default model, ~2.0 GB
ollama list                         # verify
```

## Step 2 — (Optional) configure passwords

Create a `.env` file at the repo root (already git-ignored):

```bash
POSTGRES_PASSWORD=yourpassword
RABBITMQ_PASSWORD=yourpassword
OLLAMA_MODEL=qwen2.5-coder:3b
OLLAMA_CONTEXT_LENGTH=8096
```

Defaults if skipped: Postgres `changeme`, RabbitMQ `guest`, model `qwen2.5-coder:3b`, context
length `8096`.

## Step 3 — Build and start

```bash
make up          # detached (default)
make up a=1      # attached, streams logs
# or directly:
docker compose up --build -d
```

Startup order: `postgres`/`rabbitmq` (healthy) → `migration` (runs once) → `roaster-api` →
`roaster-frontend`. `roaster-consumer` reaches Ollama via `host.docker.internal:11434`.

## Step 4 — Verify

| Service | URL |
|---|---|
| Frontend | http://localhost:5094 |
| API | http://localhost:8080 |
| API Health | http://localhost:8080/health |
| API Docs (Scalar) | http://localhost:8080/scalar/v1 |
| RabbitMQ Management | http://localhost:15672 (guest/guest) |

## Local Development (no Docker for the .NET services)

```bash
# Infra only
docker run -d --name postgres -e POSTGRES_PASSWORD=postgres -p 5432:5432 postgres:17-alpine
docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:4-management-alpine

# Migrations
dotnet run --project src/Roaster.Migration -- "Host=localhost;Database=roaster;Username=postgres;Password=postgres"

# Services (separate terminals, or make targets)
make api       # dotnet watch run — Roaster.Api
make consumer  # dotnet watch run — Roaster.Consumer
make editor    # dotnet watch run — Roaster.Editor
```

Frontend reads `Api:BaseUrl` from `src/Roaster.Editor/wwwroot/appsettings.json`.

## Build & Test

```bash
dotnet build
dotnet test

# Specific project
dotnet test tests/Roaster.Api.Tests
dotnet test tests/Roaster.Api.Tests --filter "SubmissionEndpointTests"
```

## Switching the AI Model

```bash
ollama pull qwen2.5-coder:7b
OLLAMA_MODEL=qwen2.5-coder:7b make up
```

| Model | Size | Notes |
|---|---|---|
| `qwen2.5-coder:3b` | ~2.0 GB | Default |
| `deepseek-coder:1.3b` | ~2.5 GB | Fastest, lowest quality |
| `qwen2.5-coder:7b` | ~4.7 GB | Better quality, needs 8 GB+ RAM |
| `deepseek-coder-v2:16b` | ~9.1 GB | Highest quality, needs 16 GB+ RAM |

## Useful Commands

```bash
docker compose logs -f roaster-consumer
docker compose up --build roaster-api -d   # rebuild one service
make down                                   # stop (keeps data)
docker compose down -v                      # stop + wipe Postgres volume
```

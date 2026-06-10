# Running the Project

## Prerequisites

- .NET 10 SDK
- Docker (for Postgres, RabbitMQ, Ollama — or run them natively)

---

## Option A: Docker Compose (full stack)

Spins up Postgres 17, RabbitMQ 4, runs migrations, then starts Consumer, API, and the Blazor frontend behind nginx.

```bash
docker-compose up --build
```

| Service | URL |
|---|---|
| API | http://localhost:8080 |
| Frontend (nginx) | http://localhost:5094 |
| RabbitMQ Management | http://localhost:15672 (guest/guest) |

---

## Option B: Local Development

### 1. Infrastructure (Docker)

```bash
docker run -d --name postgres -e POSTGRES_PASSWORD=postgres -p 5432:5432 postgres:17-alpine
docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:4-management-alpine
# Ollama (needed by Consumer)
OLLAMA_CONTEXT_LENGTH=8192 ollama serve   # or run as a service; default port 11434
ollama pull qwen2.5-coder:7b
```

> **Context window.** Ollama defaults to a 4096-token context (`num_ctx`), shared by the
> prompt + all submitted code + the response. The multi-agent reviewer sends every file to
> each agent, so on multi-file submissions the default silently truncates the oldest input
> (your code/instructions) instead of erroring. Set `OLLAMA_CONTEXT_LENGTH=8192` on the
> Ollama host to give reviews headroom. (This is a server-level knob — the SK Ollama
> connector cannot set `num_ctx` per request. A Modelfile `PARAMETER num_ctx` overrides it.)
>
> Reviewer sampling (temperature, max output tokens) is set in code via `Ollama:Temperature`
> / `Ollama:MaxOutputTokens` — see BACKEND.md.

### 2. Database Migrations

```bash
dotnet run --project src/Roaster.Migration -- "Host=localhost;Database=roaster;Username=postgres;Password=postgres"
# or via env:
export MIGRATION_CONNECTION_STRING="Host=localhost;Database=roaster;Username=postgres;Password=postgres"
dotnet run --project src/Roaster.Migration
```

### 3. Backend Services

Run each in a separate terminal from the repo root:

```bash
dotnet run --project src/Roaster.Api
dotnet run --project src/Roaster.Consumer
```

### 4. Frontend

```bash
cd src/Roaster.Editor
dotnet run          # http://localhost:5094, https://localhost:7052
dotnet watch run    # hot reload
```

The frontend reads `Api:BaseUrl` from `wwwroot/appsettings.json` (default `http://localhost:8080`). Update this if the API runs on a different port.

---

## Building the Full Solution

```bash
dotnet build          # build everything from repo root
dotnet test           # run all test projects
```

---

## Running Tests

```bash
# All tests
dotnet test

# Specific project
dotnet test tests/Roaster.Api.Tests
dotnet test tests/Roaster.Consumer.Tests
dotnet test tests/Roaster.Reviewer.Tests
dotnet test tests/Roaster.Data.Tests

# Filter by class or method
dotnet test tests/Roaster.Api.Tests --filter "SubmissionEndpointTests"
dotnet test tests/Roaster.Api.Tests --filter "SubmissionEndpointTests.Post_Valid_Returns202"

# Verbose output
dotnet test -v detailed
```

---

## Health Check

```
GET /health   → 200 OK (Postgres + RabbitMQ reachable)
              → 503 Service Unavailable (either dependency down)
```

---

## OpenAPI / Scalar UI

Available in development mode at:
```
http://localhost:8080/scalar/v1
```

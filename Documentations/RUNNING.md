# Running & Configuring Roaster

The single guide for running the stack and configuring it. For the data flow and backend
internals see [BACKEND.md](BACKEND.md); for the frontend see [FRONTEND.md](FRONTEND.md).

## Prerequisites

- .NET 10 SDK
- Docker (for Postgres, RabbitMQ — or run them natively)
- [Ollama](https://ollama.com/) installed **natively** (runs outside Docker so it can use the GPU / Apple Silicon Metal; Docker can't access the GPU)

---

## Ollama & the AI model

Ollama must be running and have the model pulled before the Consumer can review.

```bash
# Check if already running (expects "Ollama is running"):
curl http://localhost:11434/

# Start it (foreground, or backgrounded), with a larger context window — see note below:
OLLAMA_CONTEXT_LENGTH=8192 ollama serve
#   background: OLLAMA_CONTEXT_LENGTH=8192 ollama serve &> /tmp/ollama.log &

# Pull the default model (one-time, ~4.7 GB):
ollama pull qwen2.5-coder:7b
ollama list          # verify it appears

# Stop: Ctrl+C (foreground) or `pkill ollama` (background)
```

> **Context window.** Ollama defaults to a 4096-token context (`num_ctx`), shared by the
> prompt + all submitted code + the response. The multi-agent reviewer sends every file to
> each agent, so on multi-file submissions the default silently truncates the oldest input
> (your code/instructions) instead of erroring. Set `OLLAMA_CONTEXT_LENGTH=8192` on the
> Ollama host to give reviews headroom. (Server-level knob — the SK Ollama connector cannot
> set `num_ctx` per request. A Modelfile `PARAMETER num_ctx` overrides it.)

### Switching the model

The model is config-driven — no code edit needed. Pass it via env var:

```bash
ollama pull qwen2.5-coder:3b
OLLAMA_MODEL=qwen2.5-coder:3b make up     # or set OLLAMA_MODEL in a .env file
```

| Model | Size | Notes |
|---|---|---|
| `qwen2.5-coder:7b` | ~4.7 GB | **Default** — good quality, needs 8 GB+ RAM |
| `qwen2.5-coder:3b` | ~2.0 GB | Lighter — lower memory, weaker reviews |
| `deepseek-coder-v2:16b` | ~9.1 GB | Highest quality, needs 16 GB+ |

> Reviewer **sampling** (temperature, max output tokens) is set in code via
> `Ollama:Temperature` / `Ollama:MaxOutputTokens` — see [BACKEND.md](BACKEND.md).

---

## Option A: Docker Compose (full stack)

Spins up Postgres 17 + RabbitMQ 4, runs migrations, then starts Consumer, API, and the
Blazor frontend behind nginx.

```bash
make up        # detached (default)
make up a=1    # attached — streams logs
# or: docker compose up --build -d
```

Optionally set passwords in a `.env` file at the repo root (defaults: Postgres `changeme`,
RabbitMQ `guest`; `.env` is gitignored):

```bash
POSTGRES_PASSWORD=yourpassword
RABBITMQ_PASSWORD=yourpassword
```

Startup dependency order:

```
postgres  ──► migration ──► roaster-api ──► roaster-frontend
rabbitmq  ──────────────► roaster-api  └──► roaster-consumer
ollama (native) ───────────────────────► roaster-consumer  (via host.docker.internal)
```

| Service | URL |
|---|---|
| Frontend | http://localhost:5094 |
| API | http://localhost:8080 |
| API Health | http://localhost:8080/health |
| API Docs (Scalar) | http://localhost:8080/scalar/v1 |
| RabbitMQ Management | http://localhost:15672 (guest/guest) |

**First review:** open http://localhost:5094 → paste code → **Roast Me** → the API submits,
the Consumer picks it up, Ollama reviews, and comments appear inline (polls every 5 s, up to 5 min).

---

## Option B: Local Development

### 1. Infrastructure (Docker)

```bash
docker run -d --name postgres -e POSTGRES_PASSWORD=postgres -p 5432:5432 postgres:17-alpine
docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:4-management-alpine
# Ollama: see "Ollama & the AI model" above
```

### 2. Database migrations

```bash
dotnet run --project src/Roaster.Migration -- "Host=localhost;Database=roaster;Username=postgres;Password=postgres"
# or via env:
export MIGRATION_CONNECTION_STRING="Host=localhost;Database=roaster;Username=postgres;Password=postgres"
dotnet run --project src/Roaster.Migration
```

### 3. Backend services (separate terminals, from repo root)

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

The frontend reads `Api:BaseUrl` from `wwwroot/appsettings.json` (default `http://localhost:8080`).

---

## Building & testing

```bash
dotnet build          # build everything from repo root
dotnet test           # all test projects

# Specific project / filter
dotnet test tests/Roaster.Reviewer.Tests
dotnet test tests/Roaster.Api.Tests --filter "SubmissionEndpointTests"
dotnet test -v detailed
```

---

## Configuration reference

Config is passed to containers via environment variables in `docker-compose.yml`. The `__`
separator maps to nested JSON keys (ASP.NET Core convention).

### Roaster API

| Variable | Default | Description |
|---|---|---|
| `ConnectionStrings__Postgres` | *(set in compose)* | PostgreSQL connection string |
| `RabbitMQ__Uri` | *(set in compose)* | RabbitMQ URI (`rabbitmq://user:pass@host`) |
| `Cors__Origins__0` | `http://localhost:5094` | Allowed CORS origin |

### Roaster Consumer

| Variable | Default | Description |
|---|---|---|
| `ConnectionStrings__Postgres` | *(set in compose)* | PostgreSQL connection string |
| `RabbitMQ__Uri` | *(set in compose)* | RabbitMQ URI |
| `Ollama__Endpoint` | `http://host.docker.internal:11434` | Ollama server URL |
| `Ollama__ModelId` | `qwen2.5-coder:7b-instruct-q4_K_M` | Model to use for code review (overridable via `OLLAMA_MODEL`) |

### Frontend

Blazor WASM runs in the browser, so env vars don't apply — set the API base URL in
`src/Roaster.Editor/wwwroot/appsettings.json` (`Api:BaseUrl`) before building.

---

## Useful Docker commands

```bash
docker compose logs -f roaster-api          # follow one service's logs
docker compose up --build roaster-api -d    # rebuild & restart one service
make down                                   # stop all (data preserved)
docker compose down -v                      # stop all and WIPE the Postgres volume
```

See also `docker-clean.ps1` at the repo root (tears down, removes the volume, prunes build cache).

---

## Health & API docs

```
GET /health        → 200 {"status":"Healthy"} (Postgres + RabbitMQ reachable) | 503 if either down
GET /scalar/v1     → OpenAPI / Scalar UI (development mode)
```

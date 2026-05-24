# Running Roaster with Docker

This guide covers everything needed to run the full Roaster stack locally using Docker.

---

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) installed and running
- [Ollama](https://ollama.com/) installed natively on your machine (runs outside Docker)

---

## Step 1 — Start Ollama

Ollama runs natively on your Mac so it can use Apple Silicon (Metal). Docker cannot access the GPU.

### Check if Ollama is already running

```bash
curl http://localhost:11434/
```

If you see `Ollama is running`, skip ahead to pulling the model. If the connection is refused, start it:

```bash
ollama serve
```

Run this in a dedicated terminal and leave it open, or run it in the background:

```bash
ollama serve &> /tmp/ollama.log &
```

### Pull the model (one-time, ~2.5 GB)

```bash
ollama pull deepseek-coder:1.3b
```

Verify the model is available:

```bash
ollama list
```

You should see `deepseek-coder:1.3b` in the output.

### Stop Ollama

```bash
# If running in foreground: Ctrl+C

# If running in the background:
pkill ollama
```

---

## Step 2 — Configure passwords (optional)

The defaults work for local development. If you want to change the Postgres or RabbitMQ passwords, create a `.env` file at the repo root:

```bash
# .env  (never commit this file)
POSTGRES_PASSWORD=yourpassword
RABBITMQ_PASSWORD=yourpassword
```

If you skip this step the defaults are used:
- Postgres password: `changeme`
- RabbitMQ password: `guest`

> `.env` is already listed in `.gitignore` — it will never be committed accidentally.

---

## Step 3 — Build and start all services

From the repo root:

```bash
make up        # detached (default)
make up a=1    # attached — streams logs to the terminal
```

Or directly with Docker Compose:

```bash
docker compose up --build -d
```

Docker will start services in the correct dependency order:

```
postgres  ──► migration ──► roaster-api ──► roaster-frontend
rabbitmq  ──────────────► roaster-api
                      └──► roaster-consumer
ollama    ──────────────► roaster-consumer  (native, via host.docker.internal)
```

The first build takes a few minutes while .NET restores and compiles each project. Subsequent builds are faster due to Docker layer caching.

---

## Step 4 — Verify everything is up

| Service | URL |
|---|---|
| Frontend | http://localhost:5094 |
| API | http://localhost:8080 |
| API Health | http://localhost:8080/health |
| API Docs (Scalar) | http://localhost:8080/scalar/v1 |
| RabbitMQ Management UI | http://localhost:15672 |

The health endpoint returns `{"status":"Healthy"}` when Postgres and RabbitMQ are both reachable. If it returns `503`, check the logs (see below).

RabbitMQ Management UI credentials: `guest` / `guest` (or whatever you set in `.env`).

---

## Step 5 — Submit your first code review

1. Open http://localhost:5094
2. Paste some code into the editor
3. Click **Roast Me**
4. The API submits the code, the Consumer picks it up, Ollama reviews it
5. Comments appear inline in the editor once the review completes (polls every 5 seconds, up to 5 minutes)

---

## Useful commands

```bash
# Follow logs for a specific service
docker compose logs -f roaster-api
docker compose logs -f roaster-consumer
docker compose logs -f roaster-frontend

# Rebuild and restart a single service after a code change
docker compose up --build roaster-api -d

# Stop all services (data is preserved)
make down
# or: docker compose down

# Stop all services and delete all data (wipes the Postgres volume)
docker compose down -v
```

---

## Configuration reference

All configuration is passed to containers via environment variables in `docker-compose.yml`. The `__` separator maps to nested JSON keys (standard ASP.NET Core convention).

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
| `Ollama__ModelId` | `deepseek-coder:1.3b` | Model to use for code review |

### Frontend

The frontend is a Blazor WASM app — it runs entirely in the browser, so environment variables do not apply. The API base URL is configured in:

```
src/Roaster.Editor/wwwroot/appsettings.json
```

```json
{
  "Api": {
    "BaseUrl": "http://localhost:8080"
  }
}
```

Change this before building if your API is on a different host or port.

---

## Switching the AI model

To use a different model, pull it first:

```bash
ollama pull deepseek-coder:1.3b
```

Then pass the model name via env var (no file edit needed):

```bash
OLLAMA_MODEL=deepseek-coder:1.3b make up
```

Or set it permanently in a `.env` file at the repo root:

```bash
OLLAMA_MODEL=deepseek-coder:1.3b
```

Recommended models for Apple Silicon:

| Model | Size | Notes |
|---|---|---|
| `deepseek-coder:1.3b` | ~2.5 GB | Default — fast, low memory |
| `qwen2.5-coder:7b` | ~4.7 GB | Better quality, needs 8 GB+ RAM |
| `qwen2.5-coder:3b` | ~2.0 GB | Good balance for M1 8 GB |
| `deepseek-coder-v2:16b` | ~9.1 GB | Highest quality, needs M2 Pro / 16 GB+ |

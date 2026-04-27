# Roaster

Roaster is an AI-powered code review tool that gives you brutally honest, inline feedback directly inside a Monaco editor. Paste your code, hit **Roast Me**, and the AI drops sarcastic comments right on the lines that deserve it.

---

## How it works

1. You submit code from the browser
2. The API saves it and publishes an event to RabbitMQ
3. The Consumer picks up the event and sends the code to a local Ollama LLM
4. The AI reviews the code and saves line-by-line comments
5. The frontend polls until the review is ready and renders the comments inline in the editor
6. You can reply to any comment — the AI responds in a threaded conversation

---

## Stack

| Layer | Technology |
|---|---|
| Frontend | Blazor WebAssembly (.NET 10), Monaco Editor, MudBlazor |
| API | ASP.NET Core Minimal API (.NET 10) |
| Worker | .NET Worker Service with MassTransit |
| AI | Ollama (local LLM, runs natively on your machine) |
| Broker | RabbitMQ |
| Database | PostgreSQL with EF Core (JSONB for code payload and review comments) |
| Migrations | DbUp (plain SQL scripts, runs as a one-shot init container) |

---

## Project structure

```
src/
  Roaster.Api          — HTTP endpoints, validation, event publishing
  Roaster.Consumer     — RabbitMQ consumer, orchestrates AI review flow
  Roaster.Reviewer     — Semantic Kernel + Ollama integration
  Roaster.Data         — EF Core DbContext and entity configs
  Roaster.Contracts    — Shared events and DTOs
  Roaster.Migration    — DbUp console app, runs SQL scripts on startup
  Roaster.Editor       — Blazor WASM frontend
tests/
  Roaster.Api.Tests
  Roaster.Consumer.Tests
  Roaster.Reviewer.Tests
  Roaster.Data.Tests
docker/
  Dockerfile.api
  Dockerfile.consumer
  Dockerfile.migration
  Dockerfile.frontend
  nginx.conf
docker-compose.yml
```

---

## Getting started

See [configure.md](configure.md) for the full step-by-step setup guide.

The short version:

```bash
# 1. Start Ollama natively (runs outside Docker on Mac)
ollama serve
ollama pull qwen2.5-coder:7b

# 2. Build and start everything
docker compose up --build
```

Then open http://localhost:5094.

---

## Ports

| Service | Port |
|---|---|
| Frontend | 5094 |
| API | 8080 |
| RabbitMQ Management | 15672 |

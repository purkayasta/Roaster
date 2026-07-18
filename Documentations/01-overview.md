# 1. Overview

Roaster is an AI-powered code review tool: paste code into a Monaco editor, submit it, and a local
Ollama LLM returns sarcastic-but-accurate inline line comments. Reply to any comment and the AI
argues back in a threaded conversation.

## Stack

| Layer | Technology |
|---|---|
| Frontend | Blazor WebAssembly (.NET 10), Monaco Editor, MudBlazor 9.4 |
| API | ASP.NET Core 10 Minimal APIs |
| Worker | .NET Worker Service + MassTransit 8 |
| AI | Semantic Kernel 1.74.0 → Ollama (local LLM, default `qwen2.5-coder:3b`) |
| Broker | RabbitMQ 4 |
| Database | PostgreSQL 17 + EF Core 10 (JSONB payloads) |
| Migrations | DbUp 7 (plain SQL scripts, one-shot init container) |

## Solution Structure

```
Roaster.slnx
├── Backend/    Roaster.Api · Roaster.Consumer · Roaster.Reviewer · Roaster.Data · Roaster.Contracts · Roaster.Migration
├── Frontend/   Roaster.Editor
└── Tests/      Roaster.Api.Tests · Roaster.Consumer.Tests · Roaster.Data.Tests · Roaster.Reviewer.Tests
```

## Ports

| Service | Port |
|---|---|
| Frontend (nginx) | 5094 |
| API | 8080 |
| RabbitMQ Management | 15672 |
| Postgres | 5432 |
| Ollama | 11434 (native, not containerized) |

## Documentation Map

1. [Overview](01-overview.md) — this file
2. [Architecture](02-architecture.md) — system diagram, request lifecycle
3. [Backend](03-backend.md) — endpoints, data flow, patterns
4. [Frontend](04-frontend.md) — Blazor structure, components
5. [AI Pipeline](05-ai-pipeline.md) — Semantic Kernel + Ollama, prompts, JSON parsing
6. [Data Model](06-data-model.md) — entities, enums, ER diagram
7. [Running](07-running.md) — Docker Compose, local dev, tests

# Phase 9 — Continue

Items deferred until the core system (Phases 1–8) is stable. Each will get its own detailed plan when the time comes.

---

## Grafana + Loki

Add structured log pipeline and dashboards.

**What to add to `docker-compose.yml`:**
- `loki` service (`grafana/loki:3.0.0`)
- `grafana` service (`grafana/grafana:11.0.0`)

**What to add to API and Consumer:**
- `Serilog.Sinks.GrafanaLoki` NuGet package
- Configure Loki sink in `appsettings.json`

**Grafana dashboards to build:**
- Per-submission trace filtered by `CorrelationId`
- Average review duration from `ReviewMetrics` table
- Failure rate over time from `SubmissionEvents` table
- Queue depth from RabbitMQ management API

---

## Rate Limiting

Protect Ollama from overload when multiple users submit at the same time.

- `builder.Services.AddRateLimiter()` on `Roaster.Api`
- Fixed window policy per IP — e.g. 5 submissions per minute
- Returns `429 Too Many Requests` with a `Retry-After` header

---

## Dead-letter Queue Monitoring

MassTransit auto-creates `*-error` queues when messages exhaust all retries.

- `code-submitted-error` — failed AI reviews
- `user-thread-reply-error` — failed AI replies

Plan alerting or a Grafana panel when DLQ depth grows above a threshold.

---

## Integration Tests

End-to-end tests against a real PostgreSQL + RabbitMQ instance (Testcontainers).

- Full submission → review flow
- Thread reply → AI reply flow
- Dead-letter queue on permanent failure
- Migration idempotency (run twice, same result)

---

## Authentication

If the tool becomes multi-user, add:

- JWT bearer token on the API
- User identity stored on `Submission` and `ThreadReply`
- Submissions scoped to the authenticated user
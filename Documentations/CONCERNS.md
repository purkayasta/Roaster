# Known Concerns

## Consumer Concurrency: ConcurrentMessageLimit

**Current setting:** `ConcurrentMessageLimit = 16` in `src/Roaster.Consumer/Program.cs`.

**The concern:**

With multiple users each having their own session, `ConcurrentMessageLimit = 16` allows up to 16 Ollama reviews to run in parallel. However, the actual parallelism is bounded by what the Ollama server can handle:

- **Local Ollama (single GPU, no batching):** Ollama serializes requests internally. Setting `ConcurrentMessageLimit > 1` just moves the queue from RabbitMQ into Ollama — no real throughput gain, and it adds memory pressure from holding multiple in-flight HTTP connections.
- **Hosted/batching Ollama:** Higher concurrency genuinely helps.

**What to do when multi-user load becomes real:**

1. Profile Ollama's actual request throughput under load before tuning this number.
2. If Ollama is the bottleneck, scale Ollama horizontally (multiple instances behind a load balancer) rather than just raising `ConcurrentMessageLimit`.
3. Consider dropping `ConcurrentMessageLimit` back to `1` until you have a hosted Ollama that can actually handle parallel requests — it simplifies reasoning about consumer behavior.
4. MassTransit 8.3.6 does not expose `UsePartitioner` for regular consumers (only for Sagas). True per-SubmissionId sequential processing would require either upgrading MassTransit or implementing a keyed `SemaphoreSlim` dictionary inside the consumer. The current stale-message discard (`Status != Submitted → discard`) provides correctness but not strict ordering.

using MassTransit;
using Roaster.Contracts.Events;
using Roaster.Data.Context;
using Roaster.Data.Models;
using SerilogContext = Serilog.Context.LogContext;

namespace Roaster.Consumer.Consumers;

public class CodeSubmittedConsumer(RoasterDbContext db, IPublishEndpoint publisher,
    ILogger<CodeSubmittedConsumer> logger) : IConsumer<CodeSubmittedEvent>
{
    public async Task Consume(ConsumeContext<CodeSubmittedEvent> context)
    {
        var msg = context.Message;

        using (SerilogContext.PushProperty("CorrelationId", msg.CorrelationId))
        using (SerilogContext.PushProperty("SubmissionId", msg.SubmissionId))
        {
            var ct = context.CancellationToken;

            var submission = await db.Submissions.FindAsync(msg.SubmissionId, ct);
            if (submission is null)
            {
                logger.LogWarning("Submission {Id} not found", msg.SubmissionId);
                return;
            }

            await using var tx = await db.Database.BeginTransactionAsync(ct);

            db.SubmissionEvents.Add(new SubmissionEvent
            {
                SubmissionId  = submission.Id,
                EventType     = SubmissionEventType.ProcessingStarted,
                CorrelationId = msg.CorrelationId,
                OccurredAt    = DateTime.UtcNow
            });

            submission.Status = SubmissionStatus.Processing;
            await db.SaveChangesAsync(ct);

            // TODO Phase 4: call AI reviewer here and save Review + ReviewMetrics

            submission.Status = SubmissionStatus.Completed;
            db.SubmissionEvents.Add(new SubmissionEvent
            {
                SubmissionId  = submission.Id,
                EventType     = SubmissionEventType.ReviewCompleted,
                CorrelationId = msg.CorrelationId,
                OccurredAt    = DateTime.UtcNow
            });
            await db.SaveChangesAsync(ct);

            await tx.CommitAsync(ct);

            await publisher.Publish(new ReviewCompletedEvent(
                submission.Id, 0, msg.CorrelationId, DateTime.UtcNow));
        }
    }
}

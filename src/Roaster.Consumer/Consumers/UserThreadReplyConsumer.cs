using MassTransit;
using Roaster.Contracts.Events;
using Roaster.Data.Context;
using Roaster.Data.Models;
using SerilogContext = Serilog.Context.LogContext;

namespace Roaster.Consumer.Consumers;

public class UserThreadReplyConsumer(RoasterDbContext db,
    ILogger<UserThreadReplyConsumer> logger) : IConsumer<UserThreadReplyEvent>
{
    public async Task Consume(ConsumeContext<UserThreadReplyEvent> context)
    {
        var msg = context.Message;

        using (SerilogContext.PushProperty("CorrelationId", msg.CorrelationId))
        using (SerilogContext.PushProperty("SubmissionId", msg.SubmissionId))
        {
            var thread = await db.CommentThreads.FindAsync(msg.ThreadId);
            if (thread is null)
            {
                logger.LogWarning("Thread {Id} not found", msg.ThreadId);
                return;
            }

            // TODO Phase 4: call AI reviewer and save ThreadReply

            db.SubmissionEvents.Add(new SubmissionEvent
            {
                SubmissionId  = msg.SubmissionId,
                EventType     = SubmissionEventType.AiReplied,
                CorrelationId = msg.CorrelationId,
                OccurredAt    = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
    }
}

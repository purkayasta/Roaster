using MassTransit;
using Microsoft.EntityFrameworkCore;
using Roaster.Contracts.Events;
using Roaster.Data.Context;
using Roaster.Data.Models;
using Roaster.Reviewer.Abstractions;
using SerilogContext = Serilog.Context.LogContext;

namespace Roaster.Consumer.Consumers;

public class UserThreadReplyConsumer(
    RoasterDbContext db,
    IAiReviewService ai,
    ILogger<UserThreadReplyConsumer> logger) : IConsumer<UserThreadReplyEvent>
{
    public async Task Consume(ConsumeContext<UserThreadReplyEvent> context)
    {
        var msg = context.Message;

        using (SerilogContext.PushProperty("CorrelationId", msg.CorrelationId))
        using (SerilogContext.PushProperty("SubmissionId", msg.SubmissionId))
        {
            var ct = context.CancellationToken;

            logger.LogInformation("Processing UserThreadReplyEvent for thread {ThreadId}, submission {SubmissionId}", msg.ThreadId, msg.SubmissionId);

            var thread = await db.CommentThreads
                .Include(t => t.Replies.OrderBy(r => r.CreatedAt))
                .Include(t => t.Review)
                    .ThenInclude(r => r.Submission)
                .FirstOrDefaultAsync(t => t.Id == msg.ThreadId, ct);

            if (thread is null)
            {
                logger.LogWarning("Thread {Id} not found", msg.ThreadId);
                return;
            }

            var code            = thread.Review.Submission.Payload.Content;
            var originalComment = thread.Review.Comments
                .FirstOrDefault(c => c.LineNumber == thread.LineNumber)?.Comment ?? string.Empty;

            var history = thread.Replies
                .Select(r => new AiChatMessage(r.IsAiReply ? "assistant" : "user", r.Content ?? string.Empty))
                .ToList();

            // The latest user reply is the last entry in Replies (saved by API before publishing event)
            var userReply = history.LastOrDefault(m => m.Role == "user")?.Content ?? string.Empty;

            // Build history without the latest user message (it's passed separately)
            var priorHistory = history.Count > 0 ? history[..^1] : history;

            logger.LogInformation("Calling AI for thread {ThreadId}, line {LineNumber}, {HistoryCount} prior messages",
                msg.ThreadId, msg.LineNumber, priorHistory.Count);

            var aiReply = await ai.ReplyToThreadAsync(code, originalComment, priorHistory, userReply, ct);

            logger.LogInformation("AI reply received for thread {ThreadId}, length {Length} chars", msg.ThreadId, aiReply.Length);

            db.ThreadReplies.Add(new ThreadReply
            {
                ThreadId  = thread.Id,
                Content   = aiReply,
                IsAiReply = true,
                CreatedAt = DateTime.UtcNow
            });

            db.SubmissionEvents.Add(new SubmissionEvent
            {
                SubmissionId  = msg.SubmissionId,
                EventType     = SubmissionEventType.AiReplied,
                CorrelationId = msg.CorrelationId,
                OccurredAt    = DateTime.UtcNow
            });

            await db.SaveChangesAsync(ct);
            logger.LogInformation("AI reply saved for thread {ThreadId}", msg.ThreadId);
        }
    }
}

using System.Diagnostics;
using System.Text.Json;
using MassTransit;
using Microsoft.Extensions.Options;
using Roaster.Contracts.Events;
using Roaster.Data.Context;
using Roaster.Data.Models;
using Roaster.Reviewer.Abstractions;
using Roaster.Reviewer.Options;
using SerilogContext = Serilog.Context.LogContext;

namespace Roaster.Consumer.Consumers;

public class CodeSubmittedConsumer(
    RoasterDbContext db,
    IPublishEndpoint publisher,
    IAiReviewService ai,
    IOptions<OllamaOptions> ollamaOptions,
    ILogger<CodeSubmittedConsumer> logger) : IConsumer<CodeSubmittedEvent>
{
    public async Task Consume(ConsumeContext<CodeSubmittedEvent> context)
    {
        var msg = context.Message;

        using (SerilogContext.PushProperty("CorrelationId", msg.CorrelationId))
        using (SerilogContext.PushProperty("SubmissionId", msg.SubmissionId))
        {
            var ct = context.CancellationToken;

            logger.LogInformation("Processing CodeSubmittedEvent for submission {Id}", msg.SubmissionId);

            var submission = await db.Submissions.FindAsync(msg.SubmissionId, ct);
            if (submission is null)
            {
                logger.LogWarning("Submission {Id} not found — discarding", msg.SubmissionId);
                return;
            }

            if (submission.Status != SubmissionStatus.Submitted)
            {
                logger.LogWarning(
                    "Discarding stale CodeSubmittedEvent for SubmissionId={SubmissionId}. " +
                    "Current status={Status} is not Submitted. CorrelationId={CorrelationId}",
                    msg.SubmissionId, submission.Status, msg.CorrelationId);
                return;
            }

            var processingStartedAt = DateTime.UtcNow;

            db.SubmissionEvents.Add(new SubmissionEvent
            {
                SubmissionId  = submission.Id,
                SessionId     = submission.SessionId,
                EventType     = SubmissionEventType.ProcessingStarted,
                CorrelationId = msg.CorrelationId,
                OccurredAt    = processingStartedAt
            });
            submission.Status = SubmissionStatus.Processing;
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Submission {Id} status → Processing", msg.SubmissionId);

            var stopwatch = Stopwatch.StartNew();

            try
            {
                logger.LogInformation("Calling AI review for submission {Id}, language {Language}, code length {Length} chars",
                    msg.SubmissionId, submission.Payload.Language, submission.Payload.Content.Length);

                var aiResult = await ai.ReviewCodeAsync(
                    submission.Payload.Content, submission.Payload.Language, ct);

                logger.LogInformation("AI review completed for submission {Id} in {ElapsedMs}ms, {CommentCount} comments",
                    msg.SubmissionId, stopwatch.ElapsedMilliseconds, aiResult.LineComments.Count);

                var review = new Review
                {
                    SubmissionId = submission.Id,
                    Summary      = aiResult.Summary,
                    Comments     = aiResult.LineComments.Select(c => new ReviewLineComment
                    {
                        LineNumber = c.LineNumber,
                        Comment    = c.Comment,
                        Severity   = (ReviewSeverity)c.Severity
                    }).ToList(),
                    CreatedAt = DateTime.UtcNow
                };

                db.Reviews.Add(review);
                submission.Status = SubmissionStatus.Completed;
                await db.SaveChangesAsync(ct);

                stopwatch.Stop();

                db.ReviewMetrics.Add(new ReviewMetrics
                {
                    SubmissionId     = submission.Id,
                    ModelId          = ollamaOptions.Value.ModelId,
                    QueueWaitMs      = (int)(processingStartedAt - msg.SubmittedAt).TotalMilliseconds,
                    ReviewDurationMs = (int)stopwatch.Elapsed.TotalMilliseconds,
                    TotalDurationMs  = (int)(DateTime.UtcNow - msg.SubmittedAt).TotalMilliseconds,
                    CommentCount     = review.Comments.Count,
                    CodeLengthChars  = submission.Payload.Content.Length,
                    CreatedAt        = DateTime.UtcNow
                });

                db.SubmissionEvents.Add(new SubmissionEvent
                {
                    SubmissionId  = submission.Id,
                    SessionId     = submission.SessionId,
                    EventType     = SubmissionEventType.ReviewCompleted,
                    CorrelationId = msg.CorrelationId,
                    OccurredAt    = DateTime.UtcNow
                });

                await db.SaveChangesAsync(ct);
                logger.LogInformation("Review {ReviewId} saved for submission {Id}, status → Completed, total {TotalMs}ms",
                    review.Id, msg.SubmissionId, (int)(DateTime.UtcNow - msg.SubmittedAt).TotalMilliseconds);

                await publisher.Publish(new ReviewCompletedEvent(
                    submission.Id, review.Id, msg.CorrelationId, DateTime.UtcNow), ct);

                logger.LogInformation("ReviewCompletedEvent published for submission {Id}", msg.SubmissionId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Review failed for submission {Id}", msg.SubmissionId);
                submission.Status = SubmissionStatus.Failed;
                db.SubmissionEvents.Add(new SubmissionEvent
                {
                    SubmissionId  = submission.Id,
                    SessionId     = submission.SessionId,
                    EventType     = SubmissionEventType.Failed,
                    CorrelationId = msg.CorrelationId,
                    Metadata      = JsonSerializer.Serialize(new { error = ex.Message }),
                    OccurredAt    = DateTime.UtcNow
                });
                await db.SaveChangesAsync(ct);
                throw;
            }
        }
    }
}

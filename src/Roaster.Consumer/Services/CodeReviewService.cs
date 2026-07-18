/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
using System.Diagnostics;
using System.Text.Json;
using MassTransit;
using Microsoft.Extensions.Options;
using Roaster.Contracts.Events;
using Roaster.Data.Context;
using Roaster.Data.Models;
using Roaster.Reviewer.Abstractions;
using Roaster.Reviewer.Models;
using Roaster.Reviewer.Options;
using Event = Roaster.Data.Models.Event;

namespace Roaster.Consumer.Services;

public sealed class CodeReviewService(
    RoasterDbContext db,
    IPublishEndpoint publisher,
    IAiReviewService ai,
    IOptions<OllamaOptions> ollamaOptions,
    ILogger<CodeReviewService> logger)
{
    public async Task ProcessAsync(SubmissionCreatedEvent msg, CancellationToken ct)
    {
        logger.LogInformation("Processing SubmissionCreatedEvent for submission {Id}", msg.SubmissionId);

        var submission = await db.Submissions.FindAsync([msg.SubmissionId], ct);
        if (submission is null)
        {
            logger.LogWarning("Submission {Id} not found — discarding", msg.SubmissionId);
            return;
        }

        if (submission.Status is not SubmissionStatus.Submitted)
        {
            logger.LogWarning(
                "Discarding stale SubmissionCreatedEvent for SubmissionId={SubmissionId}. " +
                "Current status={Status} is not Submitted. CorrelationId={CorrelationId}",
                msg.SubmissionId, submission.Status, msg.CorrelationId);
            return;
        }

        var processingStartedAt = DateTime.UtcNow;

        db.Events.Add(new Event
        {
            SubmissionId = submission.Id,
            SessionId = submission.SessionId,
            EventType = EventType.ProcessingStarted,
            CorrelationId = msg.CorrelationId,
            OccurredAt = processingStartedAt
        });
        submission.Status = SubmissionStatus.Processing;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Submission {Id} status → Processing", msg.SubmissionId);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var files = submission.Payloads
                .Select(f => new CodeFileInput(f.FileName, f.Content, f.Language))
                .ToList();

            if (files.Count == 0)
            {
                var noFilesMsg = $"No files to review for submission {msg.SubmissionId}";
                logger.LogWarning(noFilesMsg);
                submission.Status = SubmissionStatus.Failed;
                db.Events.Add(new Event
                {
                    SubmissionId = submission.Id,
                    SessionId = submission.SessionId,
                    EventType = EventType.Failed,
                    CorrelationId = msg.CorrelationId,
                    Metadata = JsonSerializer.Serialize(new { message = noFilesMsg }),
                    OccurredAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync(ct);
                return;
            }

            var totalChars = files.Sum(f => f.Content.Length);
            logger.LogInformation("Calling AI review for submission {Id}, {FileCount} file(s), {TotalChars} total chars",
                msg.SubmissionId, files.Count, totalChars);

            var aiResult = await ai.ReviewCodeAsync(files, ct);

            logger.LogInformation("AI review completed for submission {Id} in {ElapsedMs}ms, status {Status}, {CommentCount} comments",
                msg.SubmissionId, stopwatch.ElapsedMilliseconds, aiResult.Status, aiResult.LineComments.Count);

            if (aiResult.Status is ReviewStatus.Failed)
            {
                var error = aiResult.Error ?? "AI returned an unparseable response with no further detail.";
                logger.LogWarning("AI review failed for submission {Id}: {Error}", msg.SubmissionId, error);
                submission.Status = SubmissionStatus.Failed;
                db.Events.Add(new Event
                {
                    SubmissionId = submission.Id,
                    SessionId = submission.SessionId,
                    EventType = EventType.Failed,
                    CorrelationId = msg.CorrelationId,
                    Metadata = JsonSerializer.Serialize(new { message = error }),
                    OccurredAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync(ct);
                return;
            }

            var firstFileName = files[0].FileName;
            var comments = new List<ReviewLineComment>(aiResult.LineComments.Count);
            foreach (var c in aiResult.LineComments)
            {
                if (c.FileName is null)
                    logger.LogWarning("AI comment at line {Line} is missing FileName; defaulting to {File}", c.LineNumber, firstFileName);
                comments.Add(new ReviewLineComment
                {
                    FileName   = (c.FileName ?? firstFileName)!,
                    LineNumber = c.LineNumber,
                    Comment    = c.Comment,
                    Severity   = (ReviewSeverity)c.Severity
                });
            }
            var review = new Review
            {
                SubmissionId = submission.Id,
                Summary = aiResult.Summary,
                Comments = comments,
                CreatedAt = DateTime.UtcNow
            };

            db.Reviews.Add(review);
            submission.Status = SubmissionStatus.Completed;
            await db.SaveChangesAsync(ct);

            stopwatch.Stop();

            db.ReviewMetrics.Add(new ReviewMetrics
            {
                ReviewId = review.Id,
                ModelId = ollamaOptions.Value.ModelId,
                QueueWaitMs = (int)(processingStartedAt - msg.SubmittedAt).TotalMilliseconds,
                ReviewDurationMs = (int)stopwatch.Elapsed.TotalMilliseconds,
                TotalDurationMs = (int)(DateTime.UtcNow - msg.SubmittedAt).TotalMilliseconds,
                CommentCount = review.Comments.Count,
                CodeLengthChars = totalChars,
                CreatedAt = DateTime.UtcNow
            });

            db.Events.Add(new Event
            {
                SubmissionId = submission.Id,
                SessionId = submission.SessionId,
                EventType = EventType.ReviewCompleted,
                CorrelationId = msg.CorrelationId,
                Metadata = JsonSerializer.Serialize(new
                {
                    promptTokens = aiResult.Usage?.PromptTokens ?? 0,
                    completionTokens = aiResult.Usage?.CompletionTokens ?? 0,
                    totalTokens = aiResult.Usage?.TotalTokens ?? 0
                }),
                OccurredAt = DateTime.UtcNow
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
            db.Events.Add(new Event
            {
                SubmissionId = submission.Id,
                SessionId = submission.SessionId,
                EventType = EventType.Failed,
                CorrelationId = msg.CorrelationId,
                Metadata = JsonSerializer.Serialize(new { error = ex.Message }),
                OccurredAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync(ct);
            throw;
        }
    }
}

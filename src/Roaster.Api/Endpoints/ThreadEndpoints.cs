using MassTransit;
using Microsoft.EntityFrameworkCore;
using Roaster.Api.Models;
using Roaster.Contracts.Dtos;
using Roaster.Contracts.Events;
using Roaster.Data.Context;
using Roaster.Data.Models;

namespace Roaster.Api.Endpoints;

public static class ThreadEndpoints
{
    public static void MapThreadEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/submissions/{submissionId:long}/reviews/{reviewId:long}/lines/{lineNumber:int}/threads",
            async (long submissionId, long reviewId, int lineNumber, ReplyRequest req, RoasterDbContext db,
                IPublishEndpoint publisher, ILoggerFactory loggerFactory, CancellationToken ct) =>
            {
                var logger = loggerFactory.CreateLogger("Threads");
                logger.LogInformation("User reply received for submission {SubmissionId}, review {ReviewId}, line {LineNumber}", submissionId, reviewId, lineNumber);

                var correlationId = await db.Submissions
                    .Where(s => s.Id == submissionId)
                    .Select(s => s.SessionId)
                    .FirstOrDefaultAsync(ct);

                await using var tx = await db.Database.BeginTransactionAsync(ct);

                var review = await db.Reviews
                    .Include(r => r.Threads)
                    .FirstOrDefaultAsync(r => r.Id == reviewId && r.SubmissionId == submissionId, ct);

                if (review is null)
                {
                    logger.LogWarning("Review {ReviewId} not found for submission {SubmissionId}", reviewId, submissionId);
                    return Results.NotFound();
                }

                if (!review.Comments.Any(c => c.LineNumber == lineNumber))
                {
                    logger.LogWarning("Line {LineNumber} does not exist in review {ReviewId}", lineNumber, reviewId);
                    return Results.BadRequest("Line number does not exist in review comments.");
                }

                var thread = review.Threads.FirstOrDefault(t => t.LineNumber == lineNumber)
                    ?? new CommentThread { ReviewId = reviewId, LineNumber = lineNumber, CreatedAt = DateTime.UtcNow };

                if (thread.Id == 0)
                {
                    db.CommentThreads.Add(thread);
                    await db.SaveChangesAsync(ct);
                    logger.LogInformation("New thread {ThreadId} created for review {ReviewId}, line {LineNumber}", thread.Id, reviewId, lineNumber);
                }
                else
                {
                    logger.LogInformation("Existing thread {ThreadId} found for line {LineNumber}", thread.Id, lineNumber);
                }

                var reply = new ThreadReply
                {
                    ThreadId = thread.Id,
                    Content = req.Content,
                    IsAiReply = false,
                    CreatedAt = DateTime.UtcNow
                };

                db.ThreadReplies.Add(reply);
                db.SubmissionEvents.Add(new SubmissionEvent
                {
                    SubmissionId  = submissionId,
                    EventType     = SubmissionEventType.UserReplied,
                    CorrelationId = correlationId,
                    OccurredAt    = DateTime.UtcNow
                });
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                logger.LogInformation("Reply saved for thread {ThreadId}, publishing UserThreadReplyEvent", thread.Id);

                await publisher.Publish(new UserThreadReplyEvent(
                    thread.Id, submissionId, lineNumber, correlationId, DateTime.UtcNow), ct);

                logger.LogInformation("UserThreadReplyEvent published for thread {ThreadId}, submission {SubmissionId}", thread.Id, submissionId);
                return Results.Accepted(null, new { threadId = thread.Id, replyId = reply.Id });
            }).WithName("CreateThread");

        app.MapGet("/api/threads/{threadId:long}/replies", async (long threadId, RoasterDbContext db, ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("Threads");
            logger.LogInformation("Fetching replies for thread {ThreadId}", threadId);

            if (!await db.CommentThreads.AnyAsync(t => t.Id == threadId, ct))
            {
                logger.LogWarning("Thread {ThreadId} not found when fetching replies", threadId);
                return Results.NotFound();
            }

            var replies = await db.ThreadReplies
                .Where(r => r.ThreadId == threadId)
                .OrderBy(r => r.CreatedAt)
                .Select(r => new ThreadReplyDto(r.Id, r.Content, r.IsAiReply, r.CreatedAt))
                .ToListAsync(ct);

            logger.LogInformation("Returning {Count} replies for thread {ThreadId}", replies.Count, threadId);
            return Results.Ok(replies);
        }).WithName("GetReplies");
    }
}

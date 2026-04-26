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
                IPublishEndpoint publisher, CancellationToken ct) =>
            {
                var review = await db.Reviews
                    .Include(r => r.Threads)
                    .FirstOrDefaultAsync(r => r.Id == reviewId && r.SubmissionId == submissionId, ct);

                if (review is null)
                    return Results.NotFound();

                if (!review.Comments.Any(c => c.LineNumber == lineNumber))
                    return Results.BadRequest("Line number does not exist in review comments.");

                var thread = review.Threads.FirstOrDefault(t => t.LineNumber == lineNumber)
                    ?? new CommentThread { ReviewId = reviewId, LineNumber = lineNumber, CreatedAt = DateTime.UtcNow };

                if (thread.Id == 0)
                {
                    db.CommentThreads.Add(thread);
                    await db.SaveChangesAsync(ct);
                }

                var reply = new ThreadReply
                {
                    ThreadId = thread.Id,
                    Content = req.Content,
                    IsAiReply = false,
                    CreatedAt = DateTime.UtcNow
                };

                db.ThreadReplies.Add(reply);
                await db.SaveChangesAsync(ct);

                await publisher.Publish(new UserThreadReplyEvent(
                    thread.Id,
                    submissionId,
                    lineNumber,
                    Guid.NewGuid(),       // CorrelationId — Phase 3 will wire this properly
                    DateTime.UtcNow), ct);

                return Results.Accepted(null, new { threadId = thread.Id, replyId = reply.Id });
            }).WithName("CreateThread");

        app.MapGet("/api/threads/{threadId:long}/replies", async (long threadId, RoasterDbContext db, CancellationToken ct) =>
        {
            var thread = await db.CommentThreads.FindAsync([threadId], ct);
            if (thread is null)
                return Results.NotFound();

            var replies = await db.ThreadReplies
                .Where(r => r.ThreadId == threadId)
                .OrderBy(r => r.CreatedAt)
                .Select(r => new ThreadReplyDto(r.Id, r.Content, r.IsAiReply, r.CreatedAt))
                .ToListAsync(ct);

            return Results.Ok(replies);
        }).WithName("GetReplies");
    }
}

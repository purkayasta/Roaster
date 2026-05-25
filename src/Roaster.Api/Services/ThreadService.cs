using MassTransit;
using Microsoft.EntityFrameworkCore;
using Roaster.Api.Models;
using Roaster.Contracts.Dtos;
using Roaster.Contracts.Events;
using Roaster.Data.Context;
using Roaster.Data.Models;

namespace Roaster.Api.Services;

public sealed class ThreadService(RoasterDbContext db, IPublishEndpoint publisher, ILogger<ThreadService> logger)
{
    public async Task<ServiceResult<ThreadCreatedDto>> AddReplyAsync(long submissionId, long reviewId, int lineNumber, ReplyRequest req, CancellationToken ct)
    {
        var correlationId = await db.Submissions
            .Where(s => s.Id == submissionId)
            .Select(s => s.SessionId)
            .FirstOrDefaultAsync(ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var review = await db.Reviews
            .Include(r => r.Threads)
            .FirstOrDefaultAsync(r => r.Id == reviewId && r.SubmissionId == submissionId, ct);

        if (review is null) return ServiceResult<ThreadCreatedDto>.NotFound();

        if (!review.Comments.Any(c => c.LineNumber == lineNumber))
            return ServiceResult<ThreadCreatedDto>.BadRequest("Line number does not exist in review comments.");

        var thread = review.Threads.FirstOrDefault(t => t.LineNumber == lineNumber)
            ?? new CommentThread { ReviewId = reviewId, LineNumber = lineNumber, CreatedAt = DateTime.UtcNow };

        if (thread.Id == 0)
        {
            db.CommentThreads.Add(thread);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Thread {ThreadId} created for review {ReviewId}, line {LineNumber}", thread.Id, reviewId, lineNumber);
        }

        var reply = new ThreadReply { ThreadId = thread.Id, Content = req.Content, IsAiReply = false, CreatedAt = DateTime.UtcNow };
        db.ThreadReplies.Add(reply);
        db.Events.Add(new Roaster.Data.Models.Event
        {
            SubmissionId  = submissionId,
            SessionId     = correlationId,
            EventType     = EventType.UserReplied,
            CorrelationId = correlationId,
            OccurredAt    = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await publisher.Publish(new ReplyEvent(thread.Id, submissionId, lineNumber, correlationId, DateTime.UtcNow), ct);
        logger.LogInformation("Reply saved for thread {ThreadId}, ReplyEvent published", thread.Id);

        return ServiceResult<ThreadCreatedDto>.Ok(new ThreadCreatedDto(thread.Id, reply.Id));
    }

    public async Task<ServiceResult<List<ThreadReplyDto>>> GetRepliesAsync(long threadId, CancellationToken ct)
    {
        if (!await db.CommentThreads.AnyAsync(t => t.Id == threadId, ct))
            return ServiceResult<List<ThreadReplyDto>>.NotFound();

        var replies = await db.ThreadReplies
            .Where(r => r.ThreadId == threadId)
            .OrderBy(r => r.CreatedAt)
            .Select(r => new ThreadReplyDto(r.Id, r.Content, r.IsAiReply, r.CreatedAt))
            .ToListAsync(ct);

        logger.LogInformation("Returning {Count} replies for thread {ThreadId}", replies.Count, threadId);
        return ServiceResult<List<ThreadReplyDto>>.Ok(replies);
    }

    public async Task<ServiceResult> ResolveThreadAsync(long threadId, CancellationToken ct)
    {
        var thread = await db.CommentThreads.FindAsync([threadId], ct);
        if (thread is null) return ServiceResult.NotFound();

        thread.Status = ThreadStatus.Resolved;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Thread {ThreadId} resolved", threadId);
        return ServiceResult.Ok();
    }
}

/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Roaster.Contracts.Events;
using Roaster.Data.Context;
using Roaster.Data.Models;
using Roaster.Reviewer.Abstractions;
using Roaster.Reviewer.Models;
using Event = Roaster.Data.Models.Event;

namespace Roaster.Consumer.Services;

public sealed class ThreadReplyService(
    RoasterDbContext db,
    IAiReviewService ai,
    ILogger<ThreadReplyService> logger)
{
    public async Task ProcessAsync(ReplyEvent msg, CancellationToken ct)
    {
        logger.LogInformation("Processing ReplyEvent for thread {ThreadId}, submission {SubmissionId}", msg.ThreadId, msg.SubmissionId);

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

        var code = string.Join("\n\n", thread.Review.Submission.Payloads.Select(f => f.Content));
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

        logger.LogInformation("AI reply received for thread {ThreadId}, length {Length} chars, {TotalTokens} total tokens",
            msg.ThreadId, aiReply.Content.Length, aiReply.Usage.TotalTokens);

        db.ThreadReplies.Add(new ThreadReply
        {
            ThreadId = thread.Id,
            Content = aiReply.Content,
            IsAiReply = true,
            CreatedAt = DateTime.UtcNow
        });

        db.Events.Add(new Event
        {
            SubmissionId = msg.SubmissionId,
            SessionId = thread.Review.Submission.SessionId,
            EventType = EventType.AiReplied,
            CorrelationId = msg.CorrelationId,
            Metadata = JsonSerializer.Serialize(new
            {
                promptTokens = aiReply.Usage.PromptTokens,
                completionTokens = aiReply.Usage.CompletionTokens,
                totalTokens = aiReply.Usage.TotalTokens
            }),
            OccurredAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(ct);
        logger.LogInformation("AI reply saved for thread {ThreadId}", msg.ThreadId);
    }
}

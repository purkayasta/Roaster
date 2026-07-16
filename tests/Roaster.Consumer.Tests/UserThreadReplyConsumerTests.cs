/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Roaster.Consumer.Processors;
using Roaster.Consumer.Services;
using Roaster.Reviewer.Models;
using Roaster.Consumer.Tests.Helpers;
using Roaster.Contracts.Events;
using Roaster.Data.Context;
using Roaster.Data.Models;
using Roaster.Reviewer.Abstractions;

namespace Roaster.Consumer.Tests;

public sealed class ReplyProcessorTests : IDisposable
{
    private readonly RoasterDbContext _db;
    private readonly Mock<IAiReviewService> _ai;
    private readonly ReplyProcessor _consumer;

    public ReplyProcessorTests()
    {
        var options = new DbContextOptionsBuilder<RoasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new RoasterDbContext(options);
        _ai = new Mock<IAiReviewService>();
        var service = new ThreadReplyService(_db, _ai.Object, NullLogger<ThreadReplyService>.Instance);
        _consumer = new ReplyProcessor(service);
    }

    public void Dispose() => _db.Dispose();

    private async Task<(Submission sub, Review review, CommentThread thread)> SeedThread(
        List<ThreadReply>? replies = null)
    {
        var session = new Session { Name = "test", CreatedAt = DateTime.UtcNow };
        _db.Sessions.Add(session);
        await _db.SaveChangesAsync();

        var sub = new Submission
        {
            SessionId = session.Id,
            Payloads  = [new Payload { FileName = "main.cs", Content = "var x = 1;", Language = "csharp" }],
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Submissions.Add(sub);
        await _db.SaveChangesAsync();

        var review = new Review
        {
            SubmissionId = sub.Id,
            Comments = [new ReviewLineComment { FileName = "main.cs", LineNumber = 5, Comment = "orig", Severity = ReviewSeverity.Medium }],
            CreatedAt = DateTime.UtcNow
        };
        _db.Reviews.Add(review);
        await _db.SaveChangesAsync();

        var thread = new CommentThread { ReviewId = review.Id, LineNumber = 5, CreatedAt = DateTime.UtcNow };
        _db.CommentThreads.Add(thread);
        await _db.SaveChangesAsync();

        if (replies is not null)
        {
            foreach (var r in replies)
            {
                r.GetType().GetProperty("ThreadId")!.SetValue(r, thread.Id);
                _db.ThreadReplies.Add(r);
            }
            await _db.SaveChangesAsync();
        }

        return (sub, review, thread);
    }

    [Fact]
    public async Task ThreadNotFound_ReturnsWithoutThrowing()
    {
        var ctx = FakeConsumeContext.For(new ReplyEvent(999L, 1L, 5, Guid.NewGuid(), DateTime.UtcNow));
        await _consumer.Consume(ctx);
    }

    [Fact]
    public async Task HappyPath_AiReplySavedWithIsAiReplyTrue()
    {
        var (sub, _, thread) = await SeedThread([
            new ThreadReply { Content = "user question", IsAiReply = false, CreatedAt = DateTime.UtcNow }
        ]);

        _ai.Setup(a => a.ReplyToThreadAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IList<AiChatMessage>>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiReplyResult("AI response here", new TokenUsage(10, 5)));

        var ctx = FakeConsumeContext.For(new ReplyEvent(thread.Id, sub.Id, 5, Guid.NewGuid(), DateTime.UtcNow));
        await _consumer.Consume(ctx);

        var aiReply = _db.ThreadReplies.FirstOrDefault(r => r.IsAiReply);
        aiReply.Should().NotBeNull();
        aiReply!.Content.Should().Be("AI response here");
    }

    [Fact]
    public async Task HappyPath_AiRepliedEventWritten()
    {
        var (sub, _, thread) = await SeedThread([
            new ThreadReply { Content = "question", IsAiReply = false, CreatedAt = DateTime.UtcNow }
        ]);

        _ai.Setup(a => a.ReplyToThreadAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IList<AiChatMessage>>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiReplyResult("reply", new TokenUsage(10, 5)));

        var ctx = FakeConsumeContext.For(new ReplyEvent(thread.Id, sub.Id, 5, Guid.NewGuid(), DateTime.UtcNow));
        await _consumer.Consume(ctx);

        _db.Events.Should().Contain(e => e.EventType == EventType.AiReplied);
    }

    [Fact]
    public async Task ConversationHistory_BuiltInChronologicalOrder()
    {
        IList<AiChatMessage>? capturedHistory = null;
        var (sub, _, thread) = await SeedThread([
            new ThreadReply { Content = "user msg 1", IsAiReply = false, CreatedAt = DateTime.UtcNow.AddMinutes(-2) },
            new ThreadReply { Content = "ai reply 1", IsAiReply = true,  CreatedAt = DateTime.UtcNow.AddMinutes(-1) },
            new ThreadReply { Content = "user msg 2", IsAiReply = false, CreatedAt = DateTime.UtcNow }
        ]);

        _ai.Setup(a => a.ReplyToThreadAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IList<AiChatMessage>>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, IList<AiChatMessage>, string, CancellationToken>(
                (_, _, history, _, _) => capturedHistory = history)
            .ReturnsAsync(new AiReplyResult("ok", new TokenUsage(10, 5)));

        var ctx = FakeConsumeContext.For(new ReplyEvent(thread.Id, sub.Id, 5, Guid.NewGuid(), DateTime.UtcNow));
        await _consumer.Consume(ctx);

        // Prior history passed (all but last user message)
        capturedHistory.Should().HaveCount(2);
        capturedHistory![0].Role.Should().Be("user");
        capturedHistory![0].Content.Should().Be("user msg 1");
        capturedHistory![1].Role.Should().Be("assistant");
        capturedHistory![1].Content.Should().Be("ai reply 1");
    }
}

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Roaster.Consumer.Consumers;
using Roaster.Consumer.Tests.Helpers;
using Roaster.Contracts.Events;
using Roaster.Data.Context;
using Roaster.Data.Models;
using Roaster.Reviewer.Abstractions;

namespace Roaster.Consumer.Tests;

public class UserThreadReplyConsumerTests : IDisposable
{
    private readonly RoasterDbContext _db;
    private readonly Mock<IAiReviewService> _ai;
    private readonly UserThreadReplyConsumer _consumer;

    public UserThreadReplyConsumerTests()
    {
        var options = new DbContextOptionsBuilder<RoasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new RoasterDbContext(options);
        _ai = new Mock<IAiReviewService>();
        _consumer = new UserThreadReplyConsumer(_db, _ai.Object, NullLogger<UserThreadReplyConsumer>.Instance);
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
            Payload   = new CodePayload { Content = "var x = 1;", Language = "csharp" },
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Submissions.Add(sub);
        await _db.SaveChangesAsync();

        var review = new Review
        {
            SubmissionId = sub.Id,
            Comments = [new ReviewLineComment { LineNumber = 5, Comment = "orig", Severity = ReviewSeverity.Medium }],
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
        var ctx = FakeConsumeContext.For(new UserThreadReplyEvent(999L, 1L, 5, Guid.NewGuid(), DateTime.UtcNow));
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
            .ReturnsAsync("AI response here");

        var ctx = FakeConsumeContext.For(new UserThreadReplyEvent(thread.Id, sub.Id, 5, Guid.NewGuid(), DateTime.UtcNow));
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
            .ReturnsAsync("reply");

        var ctx = FakeConsumeContext.For(new UserThreadReplyEvent(thread.Id, sub.Id, 5, Guid.NewGuid(), DateTime.UtcNow));
        await _consumer.Consume(ctx);

        _db.SubmissionEvents.Should().Contain(e => e.EventType == SubmissionEventType.AiReplied);
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
            .ReturnsAsync("ok");

        var ctx = FakeConsumeContext.For(new UserThreadReplyEvent(thread.Id, sub.Id, 5, Guid.NewGuid(), DateTime.UtcNow));
        await _consumer.Consume(ctx);

        // Prior history passed (all but last user message)
        capturedHistory.Should().HaveCount(2);
        capturedHistory![0].Role.Should().Be("user");
        capturedHistory![0].Content.Should().Be("user msg 1");
        capturedHistory![1].Role.Should().Be("assistant");
        capturedHistory![1].Content.Should().Be("ai reply 1");
    }
}

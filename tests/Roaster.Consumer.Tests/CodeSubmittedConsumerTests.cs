using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Roaster.Consumer.Consumers;
using Roaster.Consumer.Tests.Helpers;
using Roaster.Contracts.Events;
using Roaster.Data.Context;
using Roaster.Data.Models;
using Roaster.Reviewer.Abstractions;
using Roaster.Reviewer.Options;

namespace Roaster.Consumer.Tests;

public class CodeSubmittedConsumerTests : IDisposable
{
    private readonly RoasterDbContext _db;
    private readonly Mock<IPublishEndpoint> _publisher;
    private readonly Mock<IAiReviewService> _ai;
    private readonly CodeSubmittedConsumer _consumer;

    public CodeSubmittedConsumerTests()
    {
        var options = new DbContextOptionsBuilder<RoasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new RoasterDbContext(options);
        _publisher = new Mock<IPublishEndpoint>();
        _ai = new Mock<IAiReviewService>();

        var ollamaOpts = Options.Create(new OllamaOptions { Endpoint = "http://localhost:11434", ModelId = "test-model" });
        _consumer = new CodeSubmittedConsumer(_db, _publisher.Object, _ai.Object, ollamaOpts,
            NullLogger<CodeSubmittedConsumer>.Instance);
    }

    public void Dispose() => _db.Dispose();

    private async Task<Submission> SeedSubmission()
    {
        var sub = new Submission
        {
            Payload   = new CodePayload { Content = "var x = 1;", Language = "csharp" },
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Submissions.Add(sub);
        await _db.SaveChangesAsync();
        return sub;
    }

    [Fact]
    public async Task SubmissionNotFound_ReturnsWithoutThrowing()
    {
        var ctx = FakeConsumeContext.For(new CodeSubmittedEvent(999L, Guid.NewGuid(), DateTime.UtcNow));
        await _consumer.Consume(ctx); // should not throw
    }

    [Fact]
    public async Task HappyPath_StatusCyclesPendingToCompleted()
    {
        var sub = await SeedSubmission();
        _ai.Setup(a => a.ReviewCodeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new AiReviewResult("summary", []));

        var ctx = FakeConsumeContext.For(new CodeSubmittedEvent(sub.Id, Guid.NewGuid(), DateTime.UtcNow));
        await _consumer.Consume(ctx);

        var updated = await _db.Submissions.FindAsync(sub.Id);
        updated!.Status.Should().Be(SubmissionStatus.Completed);
    }

    [Fact]
    public async Task HappyPath_ReviewAndMetricsSaved()
    {
        var sub = await SeedSubmission();
        _ai.Setup(a => a.ReviewCodeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new AiReviewResult("great code", [new AiLineComment(3, "use const", 10)]));

        var ctx = FakeConsumeContext.For(new CodeSubmittedEvent(sub.Id, Guid.NewGuid(), DateTime.UtcNow));
        await _consumer.Consume(ctx);

        _db.Reviews.Should().HaveCount(1);
        _db.Reviews.First().Summary.Should().Be("great code");
        _db.ReviewMetrics.Should().HaveCount(1);
        _db.ReviewMetrics.First().CommentCount.Should().Be(1);
    }

    [Fact]
    public async Task HappyPath_SubmissionEventsWritten()
    {
        var sub = await SeedSubmission();
        _ai.Setup(a => a.ReviewCodeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new AiReviewResult(null, []));

        var ctx = FakeConsumeContext.For(new CodeSubmittedEvent(sub.Id, Guid.NewGuid(), DateTime.UtcNow));
        await _consumer.Consume(ctx);

        var events = _db.SubmissionEvents.Where(e => e.SubmissionId == sub.Id).ToList();
        events.Should().Contain(e => e.EventType == SubmissionEventType.ProcessingStarted);
        events.Should().Contain(e => e.EventType == SubmissionEventType.ReviewCompleted);
    }

    [Fact]
    public async Task HappyPath_ReviewCompletedEventPublished()
    {
        var sub = await SeedSubmission();
        _ai.Setup(a => a.ReviewCodeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new AiReviewResult(null, []));

        var ctx = FakeConsumeContext.For(new CodeSubmittedEvent(sub.Id, Guid.NewGuid(), DateTime.UtcNow));
        await _consumer.Consume(ctx);

        _publisher.Verify(p => p.Publish(It.IsAny<ReviewCompletedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AiThrows_StatusSetToFailed_ExceptionRethrown()
    {
        var sub = await SeedSubmission();
        _ai.Setup(a => a.ReviewCodeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ThrowsAsync(new InvalidOperationException("ollama down"));

        var ctx = FakeConsumeContext.For(new CodeSubmittedEvent(sub.Id, Guid.NewGuid(), DateTime.UtcNow));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _consumer.Consume(ctx));

        var updated = await _db.Submissions.FindAsync(sub.Id);
        updated!.Status.Should().Be(SubmissionStatus.Failed);

        _db.SubmissionEvents.Should().Contain(e => e.EventType == SubmissionEventType.Failed);
    }
}

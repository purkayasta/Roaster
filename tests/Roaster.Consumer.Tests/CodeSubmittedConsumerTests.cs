/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Roaster.Consumer.Processors;
using Roaster.Consumer.Services;
using Roaster.Consumer.Tests.Helpers;
using Roaster.Contracts.Events;
using Roaster.Data.Context;
using Roaster.Data.Models;
using Roaster.Reviewer.Abstractions;
using Roaster.Reviewer.Models;
using Roaster.Reviewer.Options;

namespace Roaster.Consumer.Tests;

public sealed class ReviewProcessorTests : IDisposable
{
    private readonly RoasterDbContext _db;
    private readonly Mock<IPublishEndpoint> _publisher;
    private readonly Mock<IAiReviewService> _ai;
    private readonly ReviewProcessor _consumer;

    public ReviewProcessorTests()
    {
        var options = new DbContextOptionsBuilder<RoasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new RoasterDbContext(options);
        _publisher = new Mock<IPublishEndpoint>();
        _ai = new Mock<IAiReviewService>();

        var ollamaOpts = Options.Create(new OllamaOptions { Endpoint = "http://localhost:11434", ModelId = "test-model" });
        var service = new CodeReviewService(_db, _publisher.Object, _ai.Object, ollamaOpts,
            NullLogger<CodeReviewService>.Instance);
        _consumer = new ReviewProcessor(service);
    }

    public void Dispose() => _db.Dispose();

    private async Task<Submission> SeedSubmission()
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
        return sub;
    }

    [Fact]
    public async Task SubmissionNotFound_ReturnsWithoutThrowing()
    {
        var ctx = FakeConsumeContext.For(new SubmissionCreatedEvent(999L, Guid.NewGuid(), DateTime.UtcNow));
        await _consumer.Consume(ctx); // should not throw
    }

    [Fact]
    public async Task HappyPath_StatusCyclesPendingToCompleted()
    {
        var sub = await SeedSubmission();
        _ai.Setup(a => a.ReviewCodeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new ReviewerResult(ReviewStatus.Success, []));

        var ctx = FakeConsumeContext.For(new SubmissionCreatedEvent(sub.Id, Guid.NewGuid(), DateTime.UtcNow));
        await _consumer.Consume(ctx);

        var updated = await _db.Submissions.FindAsync(sub.Id);
        updated!.Status.Should().Be(SubmissionStatus.Completed);
    }

    [Fact]
    public async Task HappyPath_ReviewAndMetricsSaved()
    {
        var sub = await SeedSubmission();
        _ai.Setup(a => a.ReviewCodeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new ReviewerResult(ReviewStatus.Success, [new AiLineComment(3, "use const", 10)]));

        var ctx = FakeConsumeContext.For(new SubmissionCreatedEvent(sub.Id, Guid.NewGuid(), DateTime.UtcNow));
        await _consumer.Consume(ctx);

        _db.Reviews.Should().HaveCount(1);
        _db.Reviews.First().Summary.Should().BeNull();
        _db.ReviewMetrics.Should().HaveCount(1);
        _db.ReviewMetrics.First().CommentCount.Should().Be(1);
    }

    [Fact]
    public async Task HappyPath_EventsWritten()
    {
        var sub = await SeedSubmission();
        _ai.Setup(a => a.ReviewCodeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new ReviewerResult(ReviewStatus.Success, []));

        var ctx = FakeConsumeContext.For(new SubmissionCreatedEvent(sub.Id, Guid.NewGuid(), DateTime.UtcNow));
        await _consumer.Consume(ctx);

        var events = _db.Events.Where(e => e.SubmissionId == sub.Id).ToList();
        events.Should().Contain(e => e.EventType == EventType.ProcessingStarted);
        events.Should().Contain(e => e.EventType == EventType.ReviewCompleted);
    }

    [Fact]
    public async Task HappyPath_ReviewCompletedEventPublished()
    {
        var sub = await SeedSubmission();
        _ai.Setup(a => a.ReviewCodeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new ReviewerResult(ReviewStatus.Success, []));

        var ctx = FakeConsumeContext.For(new SubmissionCreatedEvent(sub.Id, Guid.NewGuid(), DateTime.UtcNow));
        await _consumer.Consume(ctx);

        _publisher.Verify(p => p.Publish(It.IsAny<ReviewCompletedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(SubmissionStatus.Processing)]
    [InlineData(SubmissionStatus.Completed)]
    [InlineData(SubmissionStatus.Failed)]
    public async Task StaleEvent_StatusNotSubmitted_DiscardedWithoutAiCallOrPublish(SubmissionStatus staleStatus)
    {
        var sub = await SeedSubmission();
        sub.Status = staleStatus;
        await _db.SaveChangesAsync();

        var ctx = FakeConsumeContext.For(new SubmissionCreatedEvent(sub.Id, Guid.NewGuid(), DateTime.UtcNow));
        await _consumer.Consume(ctx);

        _ai.Verify(a => a.ReviewCodeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _publisher.Verify(p => p.Publish(It.IsAny<ReviewCompletedEvent>(), It.IsAny<CancellationToken>()), Times.Never);

        var updated = await _db.Submissions.FindAsync(sub.Id);
        updated!.Status.Should().Be(staleStatus);
    }

    [Fact]
    public async Task AiThrows_StatusSetToFailed_ExceptionRethrown()
    {
        var sub = await SeedSubmission();
        _ai.Setup(a => a.ReviewCodeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
           .ThrowsAsync(new InvalidOperationException("ollama down"));

        var ctx = FakeConsumeContext.For(new SubmissionCreatedEvent(sub.Id, Guid.NewGuid(), DateTime.UtcNow));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _consumer.Consume(ctx));

        var updated = await _db.Submissions.FindAsync(sub.Id);
        updated!.Status.Should().Be(SubmissionStatus.Failed);

        _db.Events.Should().Contain(e => e.EventType == EventType.Failed);
    }
}

/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;
using Roaster.Reviewer.Abstractions;
using Roaster.Reviewer.Models;
using Roaster.Reviewer.Services;

namespace Roaster.Reviewer.Tests;

public sealed class ReviewCodeAsyncTests
{
    private static readonly IReadOnlyList<CodeFileInput> CSharpFile =
        [new("main.cs", "class A { }", "csharp")];

    private static AiReviewService BuildService(params IReviewAgent[] agents)
        => new(agents, Mock.Of<IChatCompletionService>(), NullLogger<AiReviewService>.Instance);

    private static ReviewerResult Success(params AiLineComment[] comments)
        => new(ReviewStatus.Success, [.. comments], "summary");

    private static readonly ReviewerResult Failure = new(ReviewStatus.Failed, []);

    // Builds an agent that records the message it was given and returns a fixed result.
    private static IReviewAgent Agent(string name, ReviewerResult result, Action<string>? onMessage = null)
    {
        var mock = new Mock<IReviewAgent>();
        mock.SetupGet(a => a.Name).Returns(name);
        mock.Setup(a => a.ReviewAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((msg, _) => onMessage?.Invoke(msg))
            .ReturnsAsync(result);
        return mock.Object;
    }

    [Fact]
    public async Task BugAgent_ReceivesBugLanguageGuide()
    {
        string? message = null;
        var service = BuildService(Agent("Bug", Success(), m => message = m));

        await service.ReviewCodeAsync(CSharpFile);

        message.Should().Contain("C# — common bug sources");
    }

    [Fact]
    public async Task SecurityAgent_ReceivesSecurityLanguageGuide()
    {
        string? message = null;
        var service = BuildService(Agent("Security", Success(), m => message = m));

        await service.ReviewCodeAsync(CSharpFile);

        message.Should().Contain("C# — common security issues");
    }

    [Fact]
    public async Task SyntaxAgent_ReceivesQualityLanguageGuide()
    {
        string? message = null;
        var service = BuildService(Agent("Syntax", Success(), m => message = m));

        await service.ReviewCodeAsync(CSharpFile);

        message.Should().Contain("C# — quality checks");
    }

    [Fact]
    public async Task JsonOnlySubmission_AppendsNoLanguageGuide()
    {
        string? message = null;
        var service = BuildService(Agent("Bug", Success(), m => message = m));

        await service.ReviewCodeAsync([new("data.json", "{}", "json")]);

        message.Should().NotContain("LANGUAGE-SPECIFIC CHECKS");
    }

    [Fact]
    public async Task AgentMessage_ContainsSubmittedCode()
    {
        string? message = null;
        var service = BuildService(Agent("Bug", Success(), m => message = m));

        await service.ReviewCodeAsync(CSharpFile);

        message.Should().Contain("class A { }");
    }

    [Fact]
    public async Task Comment_IsTaggedWithAgentName()
    {
        var service = BuildService(Agent("Bug", Success(new AiLineComment(5, "off by one", 20))));

        var result = await service.ReviewCodeAsync(CSharpFile);

        result.LineComments.Single().Comment.Should().StartWith("[Bug] ");
    }

    [Fact]
    public async Task CommentsFromMultipleAgents_AreMerged()
    {
        var service = BuildService(
            Agent("Bug", Success(new AiLineComment(1, "bug", 20))),
            Agent("Security", Success(new AiLineComment(2, "vuln", 30))));

        var result = await service.ReviewCodeAsync(CSharpFile);

        result.LineComments.Should().HaveCount(2);
    }

    [Fact]
    public async Task AllAgentsFail_ReturnsFailed()
    {
        var service = BuildService(Agent("Bug", Failure), Agent("Security", Failure));

        var result = await service.ReviewCodeAsync(CSharpFile);

        result.Status.Should().Be(ReviewStatus.Failed);
    }

    [Fact]
    public async Task OneAgentSucceeds_ReturnsSuccess()
    {
        var service = BuildService(
            Agent("Bug", Failure),
            Agent("Security", Success(new AiLineComment(1, "vuln", 20))));

        var result = await service.ReviewCodeAsync(CSharpFile);

        result.Status.Should().Be(ReviewStatus.Success);
    }

    [Fact]
    public async Task FailedAgent_CommentsExcluded()
    {
        var service = BuildService(
            Agent("Bug", new ReviewerResult(ReviewStatus.Failed, [new AiLineComment(1, "ignored", 20)])),
            Agent("Security", Success(new AiLineComment(2, "kept", 20))));

        var result = await service.ReviewCodeAsync(CSharpFile);

        result.LineComments.Single().Comment.Should().Contain("kept");
    }

    [Fact]
    public async Task AgentResults_HasOneEntryPerAgent()
    {
        var service = BuildService(Agent("Bug", Success()), Agent("Security", Success()));

        var result = await service.ReviewCodeAsync(CSharpFile);

        result.AgentResults.Should().HaveCount(2);
    }

    [Fact]
    public async Task AgentResults_CarriesAgentName()
    {
        var service = BuildService(Agent("Bug", Success()));

        var result = await service.ReviewCodeAsync(CSharpFile);

        result.AgentResults.Single().AgentName.Should().Be("Bug");
    }

    [Fact]
    public async Task AgentResults_CarriesAgentSummary()
    {
        var service = BuildService(
            Agent("Bug", new ReviewerResult(ReviewStatus.Success, [], "found a real bug")));

        var result = await service.ReviewCodeAsync(CSharpFile);

        result.AgentResults.Single().Summary.Should().Be("found a real bug");
    }

    [Fact]
    public async Task AgentResults_CarriesCommentCount()
    {
        var service = BuildService(
            Agent("Bug", Success(new AiLineComment(1, "a", 20), new AiLineComment(2, "b", 20))));

        var result = await service.ReviewCodeAsync(CSharpFile);

        result.AgentResults.Single().CommentCount.Should().Be(2);
    }

    [Fact]
    public async Task AgentResults_IncludesFailedAgent()
    {
        var service = BuildService(Agent("Bug", Failure), Agent("Security", Success()));

        var result = await service.ReviewCodeAsync(CSharpFile);

        result.AgentResults.Should().ContainSingle(a => a.AgentName == "Bug" && a.Status == ReviewStatus.Failed);
    }
}

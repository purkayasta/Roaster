using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;
using Roaster.Reviewer.Abstractions;
using Roaster.Reviewer.Services;

namespace Roaster.Reviewer.Tests;

public class ReplyToThreadAsyncTests
{
    private static SemanticKernelAiReviewService BuildService(
        Mock<IChatCompletionService> chatMock)
        => new(chatMock.Object, NullLogger<SemanticKernelAiReviewService>.Instance);

    private static void SetupChatReply(Mock<IChatCompletionService> mock, string reply)
    {
        var content = new ChatMessageContent(AuthorRole.Assistant, reply);
        mock.Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([content]);
    }

    [Fact]
    public async Task HistoryIsReplayedInChronologicalOrder()
    {
        ChatHistory? captured = null;
        var mock = new Mock<IChatCompletionService>();
        mock.Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(), null, null, default))
            .Callback<ChatHistory, PromptExecutionSettings?, Kernel?, CancellationToken>(
                (h, _, _, _) => captured = h)
            .ReturnsAsync([new ChatMessageContent(AuthorRole.Assistant, "reply")]);

        var service = BuildService(mock);
        var history = new List<AiChatMessage>
        {
            new("user",      "first user message"),
            new("assistant", "first ai reply"),
            new("user",      "second user message"),
        };

        await service.ReplyToThreadAsync("code", "orig comment", history, "latest user message");

        // Last message added must be the latest user message
        captured!.Last().Content.Should().Be("latest user message");
        captured!.Last().Role.Should().Be(AuthorRole.User);
    }

    [Fact]
    public async Task UserMessagesMappedToUserRole()
    {
        ChatHistory? captured = null;
        var mock = new Mock<IChatCompletionService>();
        mock.Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(), null, null, default))
            .Callback<ChatHistory, PromptExecutionSettings?, Kernel?, CancellationToken>(
                (h, _, _, _) => captured = h)
            .ReturnsAsync([new ChatMessageContent(AuthorRole.Assistant, "ok")]);

        var service = BuildService(mock);
        var history = new List<AiChatMessage> { new("user", "user says hi") };

        await service.ReplyToThreadAsync("code", "comment", history, "new message");

        captured!.Should().Contain(m => m.Role == AuthorRole.User && m.Content == "user says hi");
    }

    [Fact]
    public async Task AiMessagesMappedToAssistantRole()
    {
        ChatHistory? captured = null;
        var mock = new Mock<IChatCompletionService>();
        mock.Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(), null, null, default))
            .Callback<ChatHistory, PromptExecutionSettings?, Kernel?, CancellationToken>(
                (h, _, _, _) => captured = h)
            .ReturnsAsync([new ChatMessageContent(AuthorRole.Assistant, "ok")]);

        var service = BuildService(mock);
        var history = new List<AiChatMessage> { new("assistant", "ai said this") };

        await service.ReplyToThreadAsync("code", "comment", history, "new message");

        captured!.Should().Contain(m => m.Role == AuthorRole.Assistant && m.Content == "ai said this");
    }
}

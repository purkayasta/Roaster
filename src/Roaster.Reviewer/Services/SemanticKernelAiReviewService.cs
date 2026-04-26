using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using Roaster.Reviewer.Abstractions;

namespace Roaster.Reviewer.Services;

public class SemanticKernelAiReviewService(
    IChatCompletionService chat,
    ILogger<SemanticKernelAiReviewService> logger) : IAiReviewService
{
    private const string ReviewSystemPrompt = """
        You are an expert code reviewer. Return ONLY a valid JSON object with no markdown, no prose outside the JSON:
        {
          "summary": "1-2 sentence overall assessment.",
          "comments": [
            { "lineNumber": 5, "comment": "Explanation of the issue.", "severity": 20 }
          ]
        }
        Severity values: 10 = Gentle (style), 20 = Medium (correctness/performance), 30 = Brutal (security/data loss).
        If there are no issues, return an empty comments array.
        """;

    private const string ReplySystemPrompt = """
        You are an expert code reviewer continuing a conversation with a developer.
        You have the original code, your original comment, and the conversation history.
        Reply concisely in plain text. No JSON. No markdown headers.
        Build on the conversation — do not repeat your original comment verbatim.
        """;

    public async Task<AiReviewResult> ReviewCodeAsync(string code, string language, CancellationToken ct = default)
    {
        var history = new ChatHistory(ReviewSystemPrompt);
        history.AddUserMessage($"Language: {language}\n\n{code}");

        var response = await chat.GetChatMessageContentsAsync(history, cancellationToken: ct);
        var raw = string.Concat(response.Select(m => m.Content));

        return ReviewResponseParser.Parse(raw, logger);
    }

    public async Task<string> ReplyToThreadAsync(
        string code,
        string originalComment,
        IList<AiChatMessage> history,
        string userMessage,
        CancellationToken ct = default)
    {
        var chatHistory = new ChatHistory(ReplySystemPrompt);
        chatHistory.AddUserMessage($"Code:\n{code}\n\nOriginal comment: {originalComment}");
        chatHistory.AddAssistantMessage(originalComment);

        foreach (var msg in history)
        {
            if (msg.Role == "user") chatHistory.AddUserMessage(msg.Content);
            else chatHistory.AddAssistantMessage(msg.Content);
        }

        chatHistory.AddUserMessage(userMessage);

        var response = await chat.GetChatMessageContentsAsync(chatHistory, cancellationToken: ct);
        return string.Concat(response.Select(m => m.Content));
    }
}

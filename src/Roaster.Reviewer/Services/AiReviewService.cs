/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using Roaster.Reviewer.Abstractions;
using Roaster.Reviewer.Models;
using Roaster.Reviewer.Prompts;

namespace Roaster.Reviewer.Services;

public sealed class AiReviewService(
    IChatCompletionService chat,
    ILogger<AiReviewService> logger) : IAiReviewService
{

    public async Task<ReviewerResult> ReviewCodeAsync(IReadOnlyList<CodeFileInput> files, CancellationToken ct = default)
    {
        var totalLength = files.Sum(f => f.Content.Length);
        logger.LogInformation("Starting AI code review, {FileCount} file(s), total {Length} chars", files.Count, totalLength);

        var userMessage = BuildUserMessage(files);

        var systemPrompt = PromptLoader.Reviewer;
        var history = new ChatHistory(systemPrompt);
        history.AddUserMessage(userMessage);

        var response = await chat.GetChatMessageContentsAsync(history, cancellationToken: ct);
        var raw = string.Concat(response.Select(m => m.Content));

        logger.LogInformation("AI response received, raw length {Length} chars", raw.Length);

        var result = ReviewResponseParser.Parse(raw, logger);
        logger.LogInformation("AI review parsed: {CommentCount} line comments", result.LineComments.Count);

        return result;
    }

    public async Task<string> ReplyToThreadAsync(
        string code,
        string originalComment,
        IList<AiChatMessage> history,
        string userMessage,
        CancellationToken ct = default)
    {
        logger.LogInformation("Starting AI thread reply, {HistoryCount} prior messages in history", history.Count);

        var chatHistory = new ChatHistory(PromptLoader.Replier);
        chatHistory.AddUserMessage($"Code:\n{code}\n\nOriginal comment: {originalComment}");
        chatHistory.AddAssistantMessage(originalComment);

        foreach (var msg in history)
        {
            if (msg.Role == "user") chatHistory.AddUserMessage(msg.Content);
            else chatHistory.AddAssistantMessage(msg.Content);
        }

        chatHistory.AddUserMessage(userMessage);

        var response = await chat.GetChatMessageContentsAsync(chatHistory, cancellationToken: ct);
        var reply = string.Concat(response.Select(m => m.Content));

        logger.LogInformation("AI thread reply received, length {Length} chars", reply.Length);
        return reply;
    }

    private static string BuildUserMessage(IReadOnlyList<CodeFileInput> files)
    {
        var sb = new StringBuilder();
        foreach (var file in files)
        {
            sb.AppendLine($"=== File: {file.FileName} ({file.Language}) ===");
            var lines = file.Content.Split('\n');
            for (var i = 0; i < lines.Length; i++)
                sb.AppendLine($"{i + 1,4} | {lines[i].TrimEnd('\r')}");
            sb.AppendLine();
        }
        return sb.ToString();
    }
}

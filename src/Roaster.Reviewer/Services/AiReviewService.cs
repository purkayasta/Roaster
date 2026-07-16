/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Ollama;
using Roaster.Reviewer.Abstractions;
using Roaster.Reviewer.Models;
using Roaster.Reviewer.Prompts;

namespace Roaster.Reviewer.Services;

public sealed class AiReviewService(
    IChatCompletionService chat,
    int contextLength,
    ILogger<AiReviewService> logger) : IAiReviewService
{
    private readonly OllamaPromptExecutionSettings _executionSettings = new()
    {
        ExtensionData = new Dictionary<string, object> { ["num_ctx"] = contextLength }
    };

    private static readonly Kernel TemplateKernel = Kernel.CreateBuilder().Build();
    private static readonly IPromptTemplate FileMessageTemplate =
        new KernelPromptTemplateFactory().Create(new PromptTemplateConfig(PromptLoader.FileMessage));

    public async Task<ReviewerResult> ReviewCodeAsync(IReadOnlyList<CodeFileInput> files, CancellationToken ct = default)
    {
        logger.LogInformation("Starting AI code review, {FileCount} file(s)", files.Count);

        var systemPrompt = PromptLoader.Roaster;
        var allComments = new List<AiLineComment>();
        var summaries = new List<string>();
        var totalUsage = TokenUsage.Zero;
        var anySucceeded = false;

        foreach (var file in files)
        {
            var history = new ChatHistory(systemPrompt);
            history.AddUserMessage(await BuildFileMessageAsync(file, ct));

            var response = await chat.GetChatMessageContentsAsync(history, _executionSettings, cancellationToken: ct);
            var raw = string.Concat(response.Select(m => m.Content));

            var usage = ExtractUsage(response);
            totalUsage = totalUsage.Add(usage);
            logger.LogInformation("AI response received for {FileName}, raw length {Length} chars, {PromptTokens} prompt / {CompletionTokens} completion tokens",
                file.FileName, raw.Length, usage.PromptTokens, usage.CompletionTokens);

            if (usage.PromptTokens > 0 && usage.PromptTokens >= contextLength)
                logger.LogWarning(
                    "Prompt for {FileName} used {PromptTokens} tokens, at or over the configured context length {ContextLength} — content may have been truncated by Ollama",
                    file.FileName, usage.PromptTokens, contextLength);

            var result = ReviewResponseParser.Parse(raw, logger);
            if (result.Status is ReviewStatus.Failed)
            {
                logger.LogWarning("Skipping unparseable AI response for {FileName}", file.FileName);
                continue;
            }

            anySucceeded = true;
            allComments.AddRange(result.LineComments);
            if (!string.IsNullOrWhiteSpace(result.Summary))
                summaries.Add($"{file.FileName}: {result.Summary}");
        }

        var status = anySucceeded ? ReviewStatus.Success : ReviewStatus.Failed;
        logger.LogInformation("AI review parsed: {CommentCount} line comments across {FileCount} file(s), {TotalTokens} total tokens",
            allComments.Count, files.Count, totalUsage.TotalTokens);

        return new ReviewerResult(status, allComments, summaries.Count > 0 ? string.Join('\n', summaries) : null, totalUsage);
    }

    public async Task<AiReplyResult> ReplyToThreadAsync(
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

        var response = await chat.GetChatMessageContentsAsync(chatHistory, _executionSettings, cancellationToken: ct);
        var reply = string.Concat(response.Select(m => m.Content));

        var usage = ExtractUsage(response);
        logger.LogInformation("AI thread reply received, length {Length} chars, {PromptTokens} prompt / {CompletionTokens} completion tokens",
            reply.Length, usage.PromptTokens, usage.CompletionTokens);

        if (usage.PromptTokens > 0 && usage.PromptTokens >= contextLength)
            logger.LogWarning(
                "Thread reply prompt used {PromptTokens} tokens, at or over the configured context length {ContextLength} — content may have been truncated by Ollama",
                usage.PromptTokens, contextLength);

        return new AiReplyResult(reply, usage);
    }

    private static TokenUsage ExtractUsage(IReadOnlyList<ChatMessageContent> response)
    {
        var usage = response
            .Select(m => m.Metadata?.TryGetValue("Usage", out var value) == true ? value as UsageDetails : null)
            .FirstOrDefault(u => u is not null);

        return usage is null
            ? TokenUsage.Zero
            : new TokenUsage((int)(usage.InputTokenCount ?? 0), (int)(usage.OutputTokenCount ?? 0));
    }

    private static Task<string> BuildFileMessageAsync(CodeFileInput file, CancellationToken ct)
    {
        var lines = file.Content.Split('\n');
        var numberedContent = string.Join('\n', lines.Select((line, i) => $"{i + 1,4} | {line.TrimEnd('\r')}"));

        var arguments = new KernelArguments
        {
            ["fileName"] = file.FileName,
            ["language"] = file.Language,
            ["numberedContent"] = numberedContent
        };

        return FileMessageTemplate.RenderAsync(TemplateKernel, arguments, ct);
    }
}

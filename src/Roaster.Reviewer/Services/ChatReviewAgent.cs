/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Roaster.Reviewer.Abstractions;
using Roaster.Reviewer.Models;

namespace Roaster.Reviewer.Services;

// Semantic Kernel-backed reviewer: one system prompt, one chat round-trip, parsed
// into a ReviewerResult. The SK→MAF swap replaces this class with an AIAgent-backed
// IReviewAgent; nothing else moves.
internal sealed class ChatReviewAgent(
    string name,
    string systemPrompt,
    IChatCompletionService chat,
    PromptExecutionSettings? settings,
    ILogger logger) : IReviewAgent
{
    public string Name => name;

    public async Task<ReviewerResult> ReviewAsync(string userMessage, CancellationToken ct = default)
    {
        var history = new ChatHistory(systemPrompt);
        history.AddUserMessage(userMessage);

        var response = await chat.GetChatMessageContentsAsync(history, settings, cancellationToken: ct);
        var raw = string.Concat(response.Select(m => m.Content));

        logger.LogInformation("Agent {Agent} response received, raw length {Length} chars", name, raw.Length);

        return ReviewResponseParser.Parse(raw, logger);
    }
}

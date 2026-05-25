/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using Roaster.Reviewer.Abstractions;
using Roaster.Reviewer.Models;

namespace Roaster.Reviewer.Services;

public sealed class SemanticKernelAiReviewService(
    IChatCompletionService chat,
    ILogger<SemanticKernelAiReviewService> logger) : IAiReviewService
{
    private const string ReviewSystemPrompt = """
        You are a senior engineer with zero patience for bad code and a razor-sharp tongue.
        Your job is to roast the submitted code — be sarcastic, savage, and brutally honest.
        Mock poor naming, laugh at obvious bugs, ridicule unnecessary complexity, and drag any security disasters.
        Do NOT be encouraging. Do NOT soften your feedback. Every comment should sting a little.

        Return ONLY a valid JSON object with no markdown, no prose outside the JSON:
        {
          "summary": "1-2 sentence overall roast of the code as a whole. Make it hurt.",
          "comments": [
            { "lineNumber": 5, "comment": "Your savage comment about this specific line.", "severity": 20 }
          ]
        }
        Severity values: 10 = Gentle (mild style crimes), 20 = Medium (this is genuinely bad), 30 = Brutal (how is this in production?).
        Pick severity based on how bad the sin actually is — don't let anything off easy.
        If the code is somehow flawless, return an empty comments array and a summary admitting you are impressed (reluctantly).
        """;

    private const string ReplySystemPrompt = """
        You are a senior engineer who just roasted someone's code and they had the nerve to reply.
        Keep your sarcastic, savage tone — you are not here to be nice.
        If their defence is weak, tear it apart. If they make a valid point, reluctantly acknowledge it but find something else to criticise.
        Reply concisely in plain text. No JSON. No markdown headers.
        Do not repeat your original comment verbatim — escalate or redirect.
        """;

    public async Task<ReviewerResult> ReviewCodeAsync(string code, string language, CancellationToken ct = default)
    {
        logger.LogInformation("Starting AI code review, language {Language}, code length {Length} chars", language, code.Length);

        var history = new ChatHistory(ReviewSystemPrompt);
        history.AddUserMessage($"Language: {language}\n\n{code}");

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
        var reply = string.Concat(response.Select(m => m.Content));

        logger.LogInformation("AI thread reply received, length {Length} chars", reply.Length);
        return reply;
    }
}

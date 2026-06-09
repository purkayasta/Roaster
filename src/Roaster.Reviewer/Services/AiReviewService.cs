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
    IReadOnlyList<IReviewAgent> agents,
    IChatCompletionService chat,
    ILogger<AiReviewService> logger) : IAiReviewService
{

    public async Task<ReviewerResult> ReviewCodeAsync(IReadOnlyList<CodeFileInput> files, CancellationToken ct = default)
    {
        var totalLength = files.Sum(f => f.Content.Length);
        logger.LogInformation("Starting AI code review, {AgentCount} agent(s), {FileCount} file(s), total {Length} chars",
            agents.Count, files.Count, totalLength);

        var userMessage = BuildUserMessage(files);

        // Run each specialised agent sequentially and tag its comments with the agent name.
        var comments = new List<AiLineComment>();
        var summaries = new List<string>();
        var anySucceeded = false;

        foreach (var agent in agents)
        {
            var result = await agent.ReviewAsync(userMessage, ct);
            if (result.Status is not ReviewStatus.Success)
            {
                logger.LogWarning("Agent {Agent} failed to produce a parseable review", agent.Name);
                continue;
            }

            anySucceeded = true;
            comments.AddRange(result.LineComments.Select(c => Tag(agent.Name, c)));
            if (!string.IsNullOrWhiteSpace(result.Summary))
                summaries.Add($"[{agent.Name}] {result.Summary}");

            logger.LogInformation("Agent {Agent} produced {CommentCount} comment(s)", agent.Name, result.LineComments.Count);
        }

        if (!anySucceeded)
        {
            logger.LogError("All {AgentCount} review agents failed to produce a parseable review", agents.Count);
            return new ReviewerResult(ReviewStatus.Failed, []);
        }

        logger.LogInformation("AI review merged: {CommentCount} line comments from {AgentCount} agent(s)",
            comments.Count, agents.Count);

        return new ReviewerResult(ReviewStatus.Success, comments, string.Join("\n", summaries));
    }

    // Prefix the comment with its source agent so the origin survives downstream
    // merging (CodeReviewService groups by file+line and joins comment text).
    private static AiLineComment Tag(string agentName, AiLineComment lineComment)
        => string.IsNullOrWhiteSpace(lineComment.Comment) ? lineComment : lineComment with { Comment = $"[{agentName}] {lineComment.Comment}" };

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

        // When several files are submitted they belong to one change set and may
        // reference each other. List them up front so the model reviews them as a
        // connected whole and can flag cross-file issues.
        if (files.Count > 1)
        {
            sb.AppendLine($"This submission contains {files.Count} files that belong together and may reference each other (calls, imports, shared types, data flow). Review them as one connected codebase, not in isolation.");
            sb.AppendLine();
            sb.AppendLine("Files in this submission:");
            foreach (var file in files)
                sb.AppendLine($"- {file.FileName} ({file.Language})");
            sb.AppendLine();
        }

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

namespace Roaster.Reviewer.Abstractions;

public interface IAiReviewService
{
    Task<AiReviewResult> ReviewCodeAsync(
        string code,
        string language,
        CancellationToken ct = default);

    Task<string> ReplyToThreadAsync(
        string code,
        string originalComment,
        IList<AiChatMessage> history,
        string userMessage,
        CancellationToken ct = default);
}

public record AiReviewResult(string? Summary, List<AiLineComment> LineComments);

// Severity as int to match ReviewSeverity enum: 10=Gentle, 20=Medium, 30=Brutal
public record AiLineComment(int LineNumber, string? Comment, int Severity);

public record AiChatMessage(string Role, string Content); // "user" | "assistant"

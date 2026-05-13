namespace Roaster.Data.Models;

public sealed class ReviewMetrics
{
    public long Id { get; init; }
    public long SubmissionId { get; init; }
    public string? ModelId { get; init; }
    public int QueueWaitMs { get; init; }
    public int ReviewDurationMs { get; init; }
    public int TotalDurationMs { get; init; }
    public int CommentCount { get; init; }
    public int CodeLengthChars { get; init; }
    public DateTime CreatedAt { get; init; }

    public Submission Submission { get; set; } = null!;
}

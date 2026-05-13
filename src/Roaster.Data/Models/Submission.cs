namespace Roaster.Data.Models;

public sealed class Submission
{
    public long Id { get; init; }
    public Guid SessionId { get; init; }
    public required CodePayload Payload { get; set; }
    public SubmissionStatus Status { get; set; } = SubmissionStatus.Submitted;
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; set; }

    public Session Session { get; set; } = null!;
    public Review? Review { get; set; }
    public ReviewMetrics? Metrics { get; set; }
    public ICollection<SubmissionEvent> Events { get; set; } = [];
    public ICollection<SubmissionHistory> History { get; set; } = [];
}

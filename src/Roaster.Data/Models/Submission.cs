namespace Roaster.Data.Models;

public class Submission
{
    public long Id { get; init; }
    public required CodePayload Payload { get; init; }
    public SubmissionStatus Status { get; set; } = SubmissionStatus.Pending;
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; set; }

    public Review? Review { get; set; }
    public ReviewMetrics? Metrics { get; set; }
    public ICollection<SubmissionEvent> Events { get; set; } = [];
}

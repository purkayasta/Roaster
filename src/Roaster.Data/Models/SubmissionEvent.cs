namespace Roaster.Data.Models;

public sealed class SubmissionEvent
{
    public long Id { get; init; }
    public long SubmissionId { get; init; }
    public SubmissionEventType EventType { get; init; }
    public Guid CorrelationId { get; init; }
    public string? Metadata { get; init; }  // JSONB — free-form extra data
    public DateTime OccurredAt { get; init; }

    public Submission Submission { get; set; } = null!;
}

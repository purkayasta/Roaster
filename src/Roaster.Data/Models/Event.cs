namespace Roaster.Data.Models;

public sealed class Event
{
    public long Id { get; init; }
    public long SubmissionId { get; init; }
    public Guid? SessionId { get; init; }
    public EventType EventType { get; init; }
    public Guid CorrelationId { get; init; }
    public string? Metadata { get; init; }
    public DateTime OccurredAt { get; init; }

    public Submission Submission { get; set; } = null!;
}

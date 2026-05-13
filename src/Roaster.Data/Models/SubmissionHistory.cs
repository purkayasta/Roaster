namespace Roaster.Data.Models;

public sealed class SubmissionHistory
{
    public long Id { get; init; }
    public long SubmissionId { get; init; }
    public Guid SessionId { get; init; }
    public required CodePayload Payload { get; init; }
    public DateTime ChangedAt { get; init; }

    public Submission Submission { get; set; } = null!;
    public Session Session { get; set; } = null!;
}

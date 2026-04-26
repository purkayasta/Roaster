namespace Roaster.Contracts.Events;

public record CodeSubmittedEvent(
    long SubmissionId,
    Guid CorrelationId,
    DateTime SubmittedAt);

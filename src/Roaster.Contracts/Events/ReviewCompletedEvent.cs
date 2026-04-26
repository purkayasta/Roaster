namespace Roaster.Contracts.Events;

public record ReviewCompletedEvent(
    long SubmissionId,
    long ReviewId,
    Guid CorrelationId,
    DateTime CompletedAt);

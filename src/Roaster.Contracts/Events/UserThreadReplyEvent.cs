namespace Roaster.Contracts.Events;

public record UserThreadReplyEvent(
    long ThreadId,
    long SubmissionId,
    int LineNumber,
    Guid CorrelationId,
    DateTime SubmittedAt);

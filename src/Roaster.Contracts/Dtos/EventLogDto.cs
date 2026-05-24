namespace Roaster.Contracts.Dtos;

public sealed record EventLogDto(
    long EventId,
    long SubmissionId,
    Guid? SessionId,
    string EventType,
    DateTime OccurredAt,
    string? Language,
    string? Metadata,
    string? ModelId,
    int? QueueWaitMs,
    int? ReviewDurationMs,
    int? TotalDurationMs
);

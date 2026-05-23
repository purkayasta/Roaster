namespace Roaster.Contracts.Dtos;

public sealed record SessionDetailDto(
    Guid Id,
    string Name,
    DateTime CreatedAt,
    string? CodeContent,
    string? Language,
    string? Status,
    long? SubmissionId);

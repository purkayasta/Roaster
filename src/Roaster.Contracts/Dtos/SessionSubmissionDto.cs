namespace Roaster.Contracts.Dtos;

public sealed record SessionSubmissionDto(
    long? SubmissionId,
    Guid SessionId,
    string SessionName,
    string? Language,
    string? Status,
    DateTime SessionCreatedAt);

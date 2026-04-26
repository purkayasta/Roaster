namespace Roaster.Contracts.Dtos;

public record SubmissionDto(
    long Id,
    string? Language,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt);

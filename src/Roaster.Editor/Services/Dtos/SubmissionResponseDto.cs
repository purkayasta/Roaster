namespace Roaster.Editor.Services.Dtos;

public record SubmissionResponseDto(long Id, string? Language, string Status, DateTime CreatedAt, DateTime UpdatedAt);

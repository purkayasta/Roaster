namespace Roaster.Editor.Services.Models;

public record SubmissionResponse(long Id, string? Language, string Status, DateTime CreatedAt, DateTime UpdatedAt);

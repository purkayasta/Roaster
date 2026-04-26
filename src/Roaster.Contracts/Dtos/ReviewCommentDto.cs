namespace Roaster.Contracts.Dtos;

public record ReviewCommentDto(
    int LineNumber,
    string? Comment,
    string Severity,
    long? ThreadId);

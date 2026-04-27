namespace Roaster.Editor.Services.Dtos;

public record ReviewCommentResponseDto(int LineNumber, string? Comment, string Severity, long? ThreadId);

namespace Roaster.Editor.Services.Dtos;

public record ReviewResponseDto(long Id, long SubmissionId, string? Summary, DateTime CreatedAt, List<ReviewCommentResponseDto> Comments);

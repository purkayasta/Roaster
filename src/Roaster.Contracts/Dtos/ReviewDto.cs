namespace Roaster.Contracts.Dtos;

public record ReviewDto(
    long Id,
    long SubmissionId,
    string? Summary,
    DateTime CreatedAt,
    List<ReviewCommentDto> Comments);

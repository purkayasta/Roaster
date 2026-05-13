namespace Roaster.Data.Models;

public class Review
{
    public long Id { get; init; }
    public long SubmissionId { get; init; }
    public string? Summary { get; init; }
    public List<ReviewLineComment> Comments { get; init; } = [];
    public DateTime CreatedAt { get; init; }

    public Submission Submission { get; set; } = null!;
    public ICollection<CommentThread> Threads { get; set; } = [];
}

namespace Roaster.Data.Models;

public sealed class CommentThread
{
    public long Id { get; init; }
    public long ReviewId { get; init; }
    public int LineNumber { get; init; }
    public DateTime CreatedAt { get; init; }

    public Review Review { get; set; } = null!;
    public ICollection<ThreadReply> Replies { get; set; } = [];
}

/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
namespace Roaster.Data.Models;

public sealed class CommentThread
{
    public long Id { get; init; }
    public long ReviewId { get; init; }
    public int LineNumber { get; init; }
    public DateTime CreatedAt { get; init; }
    public ThreadStatus Status { get; set; } = ThreadStatus.Unresolved;

    public Review Review { get; set; } = null!;
    public ICollection<ThreadReply> Replies { get; set; } = [];
}

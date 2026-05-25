/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
namespace Roaster.Data.Models;

public sealed class ThreadReply
{
    public long Id { get; init; }
    public long ThreadId { get; init; }
    public string? Content { get; init; }
    public bool IsAiReply { get; init; }
    public DateTime CreatedAt { get; init; }

    public CommentThread Thread { get; set; } = null!;
}

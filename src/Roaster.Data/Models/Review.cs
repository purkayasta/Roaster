/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
namespace Roaster.Data.Models;

public sealed class Review
{
    public long Id { get; init; }
    public long SubmissionId { get; init; }
    public string? Summary { get; init; }
    public List<ReviewLineComment> Comments { get; init; } = [];
    public DateTime CreatedAt { get; init; }

    public Submission Submission { get; set; } = null!;
    public ReviewMetrics? Metrics { get; set; }
    public ICollection<CommentThread> Threads { get; set; } = [];
}

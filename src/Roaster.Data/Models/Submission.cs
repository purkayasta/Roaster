/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
namespace Roaster.Data.Models;

public sealed class Submission
{
    public long Id { get; init; }
    public Guid SessionId { get; init; }
    public required CodePayload Payload { get; set; }
    public SubmissionStatus Status { get; set; } = SubmissionStatus.Submitted;
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; set; }

    public Session Session { get; set; } = null!;
    public Review? Review { get; set; }
    public ICollection<Event> Events { get; set; } = [];
}

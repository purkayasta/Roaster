/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
namespace Roaster.Editor.Services.Models;

public record EventLogResponse
{
    public long EventId { get; init; }
    public long SubmissionId { get; init; }
    public Guid? SessionId { get; init; }
    public string EventType { get; init; } = string.Empty;
    public DateTime OccurredAt { get; init; }
    public string? Language { get; init; }
    public string? Metadata { get; init; }
    public string? ModelId { get; init; }
    public int? QueueWaitMs { get; init; }
    public int? ReviewDurationMs { get; init; }
    public int? TotalDurationMs { get; init; }
}

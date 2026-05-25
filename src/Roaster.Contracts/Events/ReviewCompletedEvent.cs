/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
namespace Roaster.Contracts.Events;

public record ReviewCompletedEvent(
    long SubmissionId,
    long ReviewId,
    Guid CorrelationId,
    DateTime CompletedAt);

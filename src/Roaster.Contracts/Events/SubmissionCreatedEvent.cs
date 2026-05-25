/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
namespace Roaster.Contracts.Events;

public record SubmissionCreatedEvent(
    long SubmissionId,
    Guid CorrelationId,
    DateTime SubmittedAt);

/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
namespace Roaster.Contracts.Events;

public record ReplyEvent(
    long ThreadId,
    long SubmissionId,
    int LineNumber,
    Guid CorrelationId,
    DateTime SubmittedAt);

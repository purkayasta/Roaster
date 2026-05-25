/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
namespace Roaster.Editor.Services.Models;

public record SessionSubmissionResponse(
    long? SubmissionId,
    Guid SessionId,
    string SessionName,
    string? Language,
    string? Status,
    DateTime SessionCreatedAt);

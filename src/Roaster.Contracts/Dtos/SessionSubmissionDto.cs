/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
namespace Roaster.Contracts.Dtos;

public sealed record SessionSubmissionDto(
    long? SubmissionId,
    Guid SessionId,
    string SessionName,
    string? Status,
    DateTime SessionCreatedAt,
    int TotalComments,
    int ResolvedComments,
    bool IsMultiFile = false);

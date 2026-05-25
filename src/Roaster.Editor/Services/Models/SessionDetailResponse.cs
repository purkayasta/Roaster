/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
namespace Roaster.Editor.Services.Models;

public record SessionDetailResponse(
    Guid Id,
    string Name,
    DateTime CreatedAt,
    string? CodeContent,
    string? Language,
    string? Status,
    long? SubmissionId,
    int TotalComments,
    int ResolvedComments);

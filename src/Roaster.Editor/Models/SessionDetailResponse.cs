/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
namespace Roaster.Editor.Models;

public record SessionDetailResponse(
    Guid Id,
    string Name,
    DateTime CreatedAt,
    string? Status,
    long? SubmissionId,
    int TotalComments,
    int ResolvedComments,
    bool IsMultiFile,
    List<PayloadResponse> Payloads);

public record PayloadResponse(string? FileName, string? Content, string? Language);

/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
namespace Roaster.Contracts.Dtos;

public record ReviewCommentDto(
    int LineNumber,
    string? Comment,
    string Severity,
    long? ThreadId,
    bool IsResolved);

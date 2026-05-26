/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
namespace Roaster.Contracts.Dtos;

public sealed record SubmissionDto(
    long Id,
    List<string> FileNames,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt);

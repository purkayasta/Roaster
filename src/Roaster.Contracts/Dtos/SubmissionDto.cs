/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
namespace Roaster.Contracts.Dtos;

public record SubmissionDto(
    long Id,
    string? Language,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt);

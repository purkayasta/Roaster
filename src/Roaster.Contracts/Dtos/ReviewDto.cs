/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
namespace Roaster.Contracts.Dtos;

public record ReviewDto(
    long Id,
    long SubmissionId,
    string? Summary,
    DateTime CreatedAt,
    List<ReviewCommentDto> Comments);

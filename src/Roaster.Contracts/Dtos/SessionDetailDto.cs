/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
namespace Roaster.Contracts.Dtos;

public sealed record SessionDetailDto(
    Guid Id,
    string Name,
    DateTime CreatedAt,
    string? Status,
    long? SubmissionId,
    int TotalComments,
    int ResolvedComments,
    List<PayloadDto> Payloads);

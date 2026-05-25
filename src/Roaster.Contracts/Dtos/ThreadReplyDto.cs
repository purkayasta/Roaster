/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
namespace Roaster.Contracts.Dtos;

public record ThreadReplyDto(
    long Id,
    string? Content,
    bool IsAiReply,
    DateTime CreatedAt);

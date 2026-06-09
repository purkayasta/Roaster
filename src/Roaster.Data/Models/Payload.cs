/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
namespace Roaster.Data.Models;

public sealed record Payload
{
    public required string FileName { get; init; }
    public required string Content  { get; init; }
    public required string Language { get; init; }
}

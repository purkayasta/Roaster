/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
namespace Roaster.Data.Models;

public sealed record ReviewLineComment
{
    public required string FileName { get; init; }
    public int LineNumber { get; init; }
    public string? Comment { get; init; }
    public ReviewSeverity Severity { get; init; }
}

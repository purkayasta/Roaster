/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
namespace Roaster.Reviewer.Models;

// Severity as int to match ReviewSeverity enum: 10=Gentle, 20=Medium, 30=Brutal
public sealed record AiLineComment(int LineNumber, string? Comment, int Severity, string? FileName = null);

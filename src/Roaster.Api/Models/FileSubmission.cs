/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
using System.ComponentModel.DataAnnotations;

namespace Roaster.Api.Models;

public sealed record FileSubmission
{
    [Required]
    public string FileName { get; init; } = string.Empty;

    [Required]
    [StringLength(50000, MinimumLength = 1)]
    public string Content { get; init; } = string.Empty;

    [Required]
    [AllowedValues("csharp", "javascript", "typescript", "python", "java", "go", "rust", "cpp", "sql")]
    public string Language { get; init; } = string.Empty;
}

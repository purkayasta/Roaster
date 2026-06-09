/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
using System.ComponentModel.DataAnnotations;

namespace Roaster.Api.Models;

public sealed class CreateSessionRequest
{
    [Required]
    [StringLength(255, MinimumLength = 1)]
    public string Name { get; init; } = string.Empty;

    public bool IsMultiFile { get; init; }
}

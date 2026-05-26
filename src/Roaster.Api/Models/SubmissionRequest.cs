/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
using System.ComponentModel.DataAnnotations;

namespace Roaster.Api.Models;

public sealed class SubmissionRequest
{
    [Required]
    public Guid SessionId { get; init; }

    [Required]
    [MinLength(1)]
    public List<FileSubmission> Files { get; init; } = [];
}

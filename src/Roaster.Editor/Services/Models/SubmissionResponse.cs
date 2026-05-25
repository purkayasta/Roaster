/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
namespace Roaster.Editor.Services.Models;

public record SubmissionResponse(long Id, string? Language, string Status, DateTime CreatedAt, DateTime UpdatedAt);

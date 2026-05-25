/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
namespace Roaster.Data.Models;

public sealed class Session
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public DateTime CreatedAt { get; init; }

    public bool IsActive { get; set; } = true;

    public ICollection<Submission> Submissions { get; set; } = [];
}

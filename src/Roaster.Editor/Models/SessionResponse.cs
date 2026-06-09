/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
namespace Roaster.Editor.Models;

public record SessionResponse(Guid Id, string Name, DateTime CreatedAt, bool IsMultiFile);

/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
using Roaster.Reviewer.Models;

namespace Roaster.Reviewer.Abstractions;

// A single specialised reviewer (e.g. Security, Bug, Syntax). The implementation
// hides HOW it runs — today an SK chat completion, later a Microsoft Agent
// Framework AIAgent — so orchestration in AiReviewService never changes.
public interface IReviewAgent
{
    string Name { get; }

    Task<ReviewerResult> ReviewAsync(string userMessage, CancellationToken ct = default);
}

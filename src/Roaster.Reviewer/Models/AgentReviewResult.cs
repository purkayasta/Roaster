/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
namespace Roaster.Reviewer.Models;

// Per-agent outcome surfaced so the Consumer can record one event per agent.
// Plain data only — the Reviewer stays unaware of events or the database.
public sealed record AgentReviewResult(string AgentName, ReviewStatus Status, string? Summary, int CommentCount);

/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
namespace Roaster.Reviewer.Models;

public enum ReviewStatus { Success, Failed }

public record ReviewerResult(ReviewStatus Status, List<AiLineComment> LineComments, string? Summary = null)
{
    // One entry per agent that ran (including failures), for per-agent event logging.
    public IReadOnlyList<AgentReviewResult> AgentResults { get; init; } = [];
}

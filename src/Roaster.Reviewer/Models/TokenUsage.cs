/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
namespace Roaster.Reviewer.Models;

public sealed record TokenUsage(int PromptTokens, int CompletionTokens)
{
    public static readonly TokenUsage Zero = new(0, 0);

    public int TotalTokens => PromptTokens + CompletionTokens;

    public TokenUsage Add(TokenUsage other) =>
        new(PromptTokens + other.PromptTokens, CompletionTokens + other.CompletionTokens);
}

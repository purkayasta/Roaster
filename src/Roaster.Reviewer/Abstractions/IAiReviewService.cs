/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
using Roaster.Reviewer.Models;

namespace Roaster.Reviewer.Abstractions;

public interface IAiReviewService
{
    Task<ReviewerResult> ReviewCodeAsync(
        IReadOnlyList<CodeFileInput> files,
        CancellationToken ct = default);

    Task<AiReplyResult> ReplyToThreadAsync(
        string code,
        string originalComment,
        IList<AiChatMessage> history,
        string userMessage,
        CancellationToken ct = default);
}

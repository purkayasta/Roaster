/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
using System.Net.Http.Json;
using Roaster.Editor.Models;

namespace Roaster.Editor.Services;

public sealed class ReviewService(HttpClient client)
{
    public async Task<ApiResponse<ReviewResponse>> GetReviewAsync(long submissionId, CancellationToken ct = default)
    {
        var response = await client.GetAsync($"/api/submissions/{submissionId}/review", ct);
        return await ApiResponse<ReviewResponse>.FromAsync(response);
    }

    public async Task<ApiResponse<ThreadCreatedResponse>> PostReplyAsync(long submissionId, long reviewId, int lineNumber, string content, CancellationToken ct = default)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/submissions/{submissionId}/reviews/{reviewId}/lines/{lineNumber}/threads",
            new { Content = content }, ct);
        return await ApiResponse<ThreadCreatedResponse>.FromAsync(response);
    }

    public async Task<ApiResponse<List<ThreadReplyResponse>>> GetRepliesAsync(long threadId, CancellationToken ct = default)
    {
        var response = await client.GetAsync($"/api/threads/{threadId}/replies", ct);
        return await ApiResponse<List<ThreadReplyResponse>>.FromAsync(response);
    }

    public async Task<ApiResponse<object?>> ResolveAsync(long submissionId, long reviewId, int lineNumber, CancellationToken ct = default)
    {
        var response = await client.PatchAsync($"/api/submissions/{submissionId}/reviews/{reviewId}/lines/{lineNumber}/resolve", null, ct);
        return await ApiResponse<object?>.FromAsync(response);
    }
}

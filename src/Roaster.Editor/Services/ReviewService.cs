using System.Net.Http.Json;
using Roaster.Editor.Services.Models;

namespace Roaster.Editor.Services;

public class ReviewService(HttpClient client)
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
}

using System.Net.Http.Json;
using Roaster.Editor.Services.Dtos;

namespace Roaster.Editor.Services.Http;

public class ThreadService(IHttpClientFactory factory)
{
    private HttpClient Client => factory.CreateClient("RoasterApi");

    public async Task<CreateThreadResponseDto> CreateReplyAsync(
        long submissionId, long reviewId, int lineNumber, string content, CancellationToken ct = default)
    {
        var url = $"/api/submissions/{submissionId}/reviews/{reviewId}/lines/{lineNumber}/threads";
        var response = await Client.PostAsJsonAsync(url, new { content }, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CreateThreadResponseDto>(ct)
            ?? throw new InvalidOperationException("Empty response from threads endpoint.");
    }

    public async Task<List<ThreadReplyDto>> GetRepliesAsync(long threadId, CancellationToken ct = default)
    {
        var response = await Client.GetAsync($"/api/threads/{threadId}/replies", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return [];
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ThreadReplyDto>>(ct) ?? [];
    }
}

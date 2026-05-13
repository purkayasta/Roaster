using System.Net.Http.Json;
using Roaster.Editor.Services.Dtos;

namespace Roaster.Editor.Services.Http;

public class ReviewService(IHttpClientFactory factory)
{
    private HttpClient Client => factory.CreateClient("RoasterApi");

    // Returns null when review is not ready yet (204 No Content)
    public async Task<ReviewResponseDto?> GetAsync(long submissionId, CancellationToken ct = default)
    {
        var response = await Client.GetAsync($"/api/submissions/{submissionId}/review", ct);

        if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return null;
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ReviewResponseDto>(ct);
    }
}

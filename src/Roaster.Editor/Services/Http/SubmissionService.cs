using System.Net.Http.Json;
using Roaster.Editor.Services.Dtos;

namespace Roaster.Editor.Services.Http;

public class SubmissionService(IHttpClientFactory factory)
{
    private HttpClient Client => factory.CreateClient("RoasterApi");

    public async Task<SubmissionResponseDto> CreateAsync(string code, string language, CancellationToken ct = default)
    {
        var response = await Client.PostAsJsonAsync("/api/submissions", new { code, language }, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SubmissionResponseDto>(ct)
            ?? throw new InvalidOperationException("Empty response from submissions endpoint.");
    }

    public async Task<SubmissionResponseDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var response = await Client.GetAsync($"/api/submissions/{id}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SubmissionResponseDto>(ct);
    }
}

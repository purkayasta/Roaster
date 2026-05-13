using System.Net.Http.Json;
using Roaster.Editor.Services.Models;

namespace Roaster.Editor.Services;

public class SubmissionService(HttpClient client)
{
    public async Task<List<SubmissionResponse>> GetAllAsync(int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var response = await client.GetAsync($"/api/submissions?page={page}&pageSize={pageSize}", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<SubmissionResponse>>(ct) ?? [];
    }
}

using System.Net.Http.Json;
using Roaster.Editor.Services.Models;

namespace Roaster.Editor.Services;

public class SessionService(HttpClient client)
{
    public async Task<List<SessionSubmissionResponse>> GetAllAsync(int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var response = await client.GetAsync($"/api/sessions?page={page}&pageSize={pageSize}", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<SessionSubmissionResponse>>(ct) ?? [];
    }

    public async Task<SessionDetailResponse> GetByIdAsync(Guid sessionId, CancellationToken ct = default)
    {
        var response = await client.GetAsync($"/api/sessions/{sessionId}", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SessionDetailResponse>(ct)
            ?? throw new InvalidOperationException("Failed to deserialize session response.");
    }

    public async Task<SessionResponse> CreateAsync(string name, CancellationToken ct = default)
    {
        var response = await client.PostAsJsonAsync("/api/sessions", new { Name = name }, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SessionResponse>(ct)
            ?? throw new InvalidOperationException("Failed to deserialize session response.");
    }

    public async Task DeleteAsync(Guid sessionId, CancellationToken ct = default)
    {
        var response = await client.DeleteAsync($"/api/sessions/{sessionId}", ct);
        response.EnsureSuccessStatusCode();
    }
}

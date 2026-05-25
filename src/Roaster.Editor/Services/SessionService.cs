/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
using System.Net.Http.Json;
using Roaster.Editor.Services.Models;

namespace Roaster.Editor.Services;

public sealed class SessionService(HttpClient client)
{
    public async Task<ApiResponse<List<SessionSubmissionResponse>>> GetAllAsync(int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var response = await client.GetAsync($"/api/sessions?page={page}&pageSize={pageSize}", ct);
        return await ApiResponse<List<SessionSubmissionResponse>>.FromAsync(response);
    }

    public async Task<ApiResponse<SessionDetailResponse>> GetByIdAsync(Guid sessionId, CancellationToken ct = default)
    {
        var response = await client.GetAsync($"/api/sessions/{sessionId}", ct);
        return await ApiResponse<SessionDetailResponse>.FromAsync(response);
    }

    public async Task<ApiResponse<SessionResponse>> CreateAsync(string name, CancellationToken ct = default)
    {
        var response = await client.PostAsJsonAsync("/api/sessions", new { Name = name }, ct);
        return await ApiResponse<SessionResponse>.FromAsync(response);
    }

    public async Task<ApiResponse<object?>> DeleteAsync(Guid sessionId, CancellationToken ct = default)
    {
        var response = await client.DeleteAsync($"/api/sessions/{sessionId}", ct);
        return await ApiResponse<object?>.FromAsync(response);
    }
}

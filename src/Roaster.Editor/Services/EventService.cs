/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
using Roaster.Editor.Models;

namespace Roaster.Editor.Services;

public sealed class EventService(HttpClient client)
{
    public async Task<ApiResponse<List<EventLogResponse>>> GetAllAsync(
        int page = 1, int pageSize = 30, Guid? sessionId = null, CancellationToken ct = default)
    {
        var url = $"/api/events?page={page}&pageSize={pageSize}";
        if (sessionId.HasValue) url += $"&sessionId={sessionId}";
        var response = await client.GetAsync(url, ct);
        return await ApiResponse<List<EventLogResponse>>.FromAsync(response);
    }
}

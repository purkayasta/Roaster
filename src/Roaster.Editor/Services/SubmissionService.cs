/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
using System.Net.Http.Json;
using Roaster.Editor.Services.Models;

namespace Roaster.Editor.Services;

public sealed class SubmissionService(HttpClient client)
{
    public async Task<ApiResponse<SubmissionResponse>> SubmitAsync(Guid sessionId, string code, string language, CancellationToken ct = default)
    {
        var response = await client.PostAsJsonAsync("/api/submissions", new { SessionId = sessionId, Code = code, Language = language }, ct);
        return await ApiResponse<SubmissionResponse>.FromAsync(response);
    }
}

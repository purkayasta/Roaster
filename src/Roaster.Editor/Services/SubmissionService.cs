/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
using System.Net.Http.Json;
using Roaster.Editor.Models;

namespace Roaster.Editor.Services;

public sealed record FileSubmissionRequest(string FileName, string Content, string Language);

public sealed class SubmissionService(HttpClient client)
{
    public async Task<ApiResponse<SubmissionResponse>> SubmitAsync(Guid sessionId, IReadOnlyList<FileSubmissionRequest> files, CancellationToken ct = default)
    {
        var response = await client.PostAsJsonAsync("/api/submissions", new { SessionId = sessionId, Files = files }, ct);
        return await ApiResponse<SubmissionResponse>.FromAsync(response);
    }
}

/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
using System.Net.Http.Json;

namespace Roaster.Editor.Services.Models;

public record ApiResponse<T>
{
    public bool IsSuccess { get; init; }
    public int StatusCode { get; init; }
    public T? Data { get; init; }
    public string? Error { get; init; }

    public static async Task<ApiResponse<T>> FromAsync(HttpResponseMessage response)
    {
        var statusCode = (int)response.StatusCode;

        if (response.IsSuccessStatusCode)
        {
            var data = response.StatusCode == System.Net.HttpStatusCode.NoContent
                ? default
                : await response.Content.ReadFromJsonAsync<T>();
            return new() { IsSuccess = true, StatusCode = statusCode, Data = data };
        }

        var error = await response.Content.ReadAsStringAsync();
        return new()
        {
            IsSuccess  = false,
            StatusCode = statusCode,
            Error      = string.IsNullOrWhiteSpace(error) ? response.ReasonPhrase : error.Trim('"')
        };
    }
}

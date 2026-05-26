/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Roaster.Reviewer.Models;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Roaster.Reviewer.Tests")]

namespace Roaster.Reviewer.Services;

internal static class ReviewResponseParser
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    internal static ReviewerResult Parse(string raw, ILogger logger)
    {
        if (TryParseJson(raw, out var result)) return result!;

        var stripped = Regex.Replace(raw, @"```json\s*([\s\S]*?)\s*```", "$1").Trim();
        if (TryParseJson(stripped, out result)) return result!;

        var first = raw.IndexOf('{');
        var last  = raw.LastIndexOf('}');
        if (first >= 0 && last > first && TryParseJson(raw[first..(last + 1)], out result))
            return result!;

        logger.LogError("Failed to parse AI review response: {Raw}", raw.Length > 500 ? raw[..500] : raw);
        return new ReviewerResult(ReviewStatus.Failed, []);
    }

    private static bool TryParseJson(string raw, out ReviewerResult? result)
    {
        result = null;
        try
        {
            var response = JsonSerializer.Deserialize<AiReviewResponse>(raw, JsonOptions);
            if (response is null) return false;

            var comments = (response.Comments ?? [])
                .Where(c => c.LineNumber > 0)
                .Select(c => new AiLineComment(c.LineNumber, c.Comment, c.Severity, c.FileName))
                .ToList();

            result = new ReviewerResult(ReviewStatus.Success, comments, response.Summary);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

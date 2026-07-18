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
        if (TryParseJson(raw, out var result, out var lastError)) return result!;

        var stripped = Regex.Replace(raw, @"```json\s*([\s\S]*?)\s*```", "$1").Trim();
        if (TryParseJson(stripped, out result, out lastError)) return result!;

        var first = raw.IndexOf('{');
        var last  = raw.LastIndexOf('}');
        if (first >= 0 && last > first && TryParseJson(raw[first..(last + 1)], out result, out lastError))
            return result!;

        // Small models often emit raw newlines/tabs inside string values, which is invalid JSON.
        if (first >= 0 && last > first && TryParseJson(EscapeControlCharsInStrings(raw[first..(last + 1)]), out result, out lastError))
            return result!;

        var snippet = raw.Length > 500 ? raw[..500] : raw;
        var error = string.IsNullOrEmpty(raw)
            ? "AI returned an empty response."
            : $"{lastError}. Raw response: {snippet}";

        logger.LogError("Failed to parse AI review response: {Error}", error);
        return new ReviewerResult(ReviewStatus.Failed, [], Error: error);
    }

    private static string EscapeControlCharsInStrings(string raw)
    {
        var sb = new System.Text.StringBuilder(raw.Length);
        var inString = false;
        var escaped = false;

        foreach (var c in raw)
        {
            if (inString && !escaped && c is '\n' or '\r' or '\t')
            {
                sb.Append(c switch { '\n' => "\\n", '\r' => "\\r", _ => "\\t" });
                continue;
            }

            sb.Append(c);

            if (escaped) { escaped = false; continue; }
            if (c == '\\') { escaped = true; continue; }
            if (c == '"') inString = !inString;
        }

        return sb.ToString();
    }

    private static bool TryParseJson(string raw, out ReviewerResult? result, out string? error)
    {
        result = null;
        error = null;
        try
        {
            var response = JsonSerializer.Deserialize<AiReviewResponse>(raw, JsonOptions);
            if (response is null)
            {
                error = "AI response deserialized to null.";
                return false;
            }

            var comments = (response.Comments ?? [])
                .Where(c => c.LineNumber > 0)
                .Select(c => new AiLineComment(c.LineNumber, c.Comment, c.Severity, c.FileName))
                .ToList();

            result = new ReviewerResult(ReviewStatus.Success, comments, response.Summary);
            return true;
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }
}

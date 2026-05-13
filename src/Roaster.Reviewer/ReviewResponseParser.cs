using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Roaster.Reviewer.Abstractions;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Roaster.Reviewer.Tests")]

namespace Roaster.Reviewer;

internal static class ReviewResponseParser
{
    internal static AiReviewResult Parse(string raw, ILogger logger)
    {
        if (TryParseJson(raw, out var result)) return result!;

        var stripped = Regex.Replace(raw, @"```json\s*([\s\S]*?)\s*```", "$1").Trim();
        if (TryParseJson(stripped, out result)) return result!;

        var first = raw.IndexOf('{');
        var last  = raw.LastIndexOf('}');
        if (first >= 0 && last > first && TryParseJson(raw[first..(last + 1)], out result))
            return result!;

        logger.LogWarning("Failed to parse AI review response: {Raw}", raw.Length > 500 ? raw[..500] : raw);
        return new AiReviewResult(null, []);
    }

    private static bool TryParseJson(string raw, out AiReviewResult? result)
    {
        result = null;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            var summary  = root.TryGetProperty("summary",  out var s) ? s.GetString() : null;
            var comments = new List<AiLineComment>();

            if (root.TryGetProperty("comments", out var arr))
            {
                foreach (var item in arr.EnumerateArray())
                {
                    var line     = item.TryGetProperty("lineNumber", out var ln) ? ln.GetInt32() : 0;
                    var comment  = item.TryGetProperty("comment",    out var c)  ? c.GetString()  : null;
                    var severity = item.TryGetProperty("severity",   out var sv) ? sv.GetInt32()  : 20;

                    if (line == 0) continue; // line 0 is invalid — skip

                    comments.Add(new AiLineComment(line, comment, severity));
                }
            }

            result = new AiReviewResult(summary, comments);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
using System.Text.RegularExpressions;
using BlazorMonaco.Editor;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using Roaster.Editor.Models;
using EditorGlobal = BlazorMonaco.Editor.Global;

namespace Roaster.Editor.Utils;

public static class CodeUtils
{
    public const string EditorId = "code-editor-main";
    public const string DefaultBoilerplate = "// Paste your code here and click Submit to get a review.\n";
    public const string DefaultLanguage = "csharp";
    public const string DefaultTheme = "vs-dark";

    public static readonly IReadOnlyList<string> Languages =
    [
        "csharp", "javascript", "typescript", "json"
    ];

    private static readonly (string Key, string Label)[] BuiltInThemes =
    [
        ("vs-dark", "Dark"),
        ("vs",      "Light"),
    ];

    public static IEnumerable<(string Key, string Label)> Themes =>
        [.. BuiltInThemes, .. ThemeFactory.CustomThemes];

    public static StandaloneEditorConstructionOptions BuildEditorOptions(string language, string theme, string initialValue = "") => new()
    {
        AutomaticLayout = true,
        Language = language,
        Value = initialValue,
        Theme = theme,
        WordWrap = "on",
        ScrollBeyondLastLine = false,
        Minimap = new EditorMinimapOptions { Enabled = false },
        LineNumbers = "on",
        FontSize = 14,
        GlyphMargin = true,
        FontFamily = "'Geist Mono', 'Cascadia Code', 'Fira Code', monospace",
    };

    public static async Task SetLanguageAsync(IJSRuntime js, StandaloneCodeEditor editor, string language)
    {
        var model = await editor.GetModel();
        await EditorGlobal.SetModelLanguage(js, model, language);
    }

    public static Task SetThemeAsync(IJSRuntime js, string theme) =>
        EditorGlobal.SetTheme(js, theme);

    public static Color GetSeverityColor(string severity) => severity.ToLowerInvariant() switch
    {
        "brutal" => Color.Error,
        "medium" => Color.Warning,
        "gentle" => Color.Info,
        _ => Color.Default
    };

    public static string GetSeverityIcon(string severity) => severity.ToLowerInvariant() switch
    {
        "brutal" => Icons.Material.Filled.Dangerous,
        "medium" => Icons.Material.Filled.Warning,
        "gentle" => Icons.Material.Filled.Info,
        _ => Icons.Material.Filled.Comment
    };

    // The reviewer prefixes each comment with its source agent, e.g. "[Security] ...".
    // Merged comments (several agents on one line) concatenate these with blank lines.
    public static Color GetCategoryColor(string category) => category.ToLowerInvariant() switch
    {
        "security" => Color.Error,
        "bug"      => Color.Warning,
        "syntax"   => Color.Info,
        _          => Color.Default
    };

    private static readonly Regex CategoryLead = new(@"^\[(Security|Bug|Syntax)\]\s*", RegexOptions.Compiled);

    // Split a (possibly merged) comment into its tagged segments so each can show a
    // coloured category chip with the "[Tag]" prefix stripped from the text.
    public static IReadOnlyList<(string? Category, string Text)> SplitTaggedComment(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment)) return [];

        var segments = Regex.Split(comment, @"(?=\[(?:Security|Bug|Syntax)\])")
            .Where(s => !string.IsNullOrWhiteSpace(s));

        var result = new List<(string?, string)>();
        foreach (var segment in segments)
        {
            var match = CategoryLead.Match(segment);
            if (match.Success)
                result.Add((match.Groups[1].Value, segment[match.Length..].Trim()));
            else
                result.Add((null, segment.Trim()));
        }
        return result;
    }

    // --- Decoration / layout interop ---

    public static Task<string[]> AddReviewDecorationsAsync(IJSRuntime js, IEnumerable<ReviewCommentResponse> comments)
    {
        var args = comments.Select(c => new { lineNumber = c.LineNumber, severity = c.Severity }).ToArray();
        return js.InvokeAsync<string[]>("RoasterReview.addDecorations", EditorId, args).AsTask();
    }

    public static Task ClearDecorationsAsync(IJSRuntime js, string[] decorationIds) =>
        js.InvokeVoidAsync("RoasterReview.clearDecorations", EditorId, decorationIds).AsTask();

    public static Task AttachSplitterAsync(IJSRuntime js, ElementReference handle, ElementReference panel) =>
        js.InvokeVoidAsync("RoasterSplit.attach", handle, panel, 320, 720).AsTask();
}

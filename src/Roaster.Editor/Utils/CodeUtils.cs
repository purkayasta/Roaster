/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
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

    // --- ViewZone / decoration interop ---

    public static Task<string?> AddReviewZoneAsync(IJSRuntime js, int lineNumber, ElementReference domRef) =>
        js.InvokeAsync<string?>("RoasterReview.addZone", EditorId, lineNumber, domRef).AsTask();

    public static Task UpdateZoneHeightAsync(IJSRuntime js, string zoneId, ElementReference domRef) =>
        js.InvokeVoidAsync("RoasterReview.updateZoneHeight", EditorId, zoneId, domRef).AsTask();

    public static Task RemoveZonesAsync(IJSRuntime js, IEnumerable<string> zoneIds) =>
        js.InvokeVoidAsync("RoasterReview.removeZones", EditorId, zoneIds.ToArray()).AsTask();

    public static Task<string[]> AddReviewDecorationsAsync(IJSRuntime js, IEnumerable<ReviewCommentResponse> comments)
    {
        var args = comments.Select(c => new { lineNumber = c.LineNumber, severity = c.Severity }).ToArray();
        return js.InvokeAsync<string[]>("RoasterReview.addDecorations", EditorId, args).AsTask();
    }

    public static Task ClearDecorationsAsync(IJSRuntime js, string[] decorationIds) =>
        js.InvokeVoidAsync("RoasterReview.clearDecorations", EditorId, decorationIds).AsTask();

    public static Task SyncOverlayAsync(IJSRuntime js) =>
        js.InvokeVoidAsync("RoasterReview.syncOverlay", EditorId, "roaster-comment-overlay").AsTask();
}

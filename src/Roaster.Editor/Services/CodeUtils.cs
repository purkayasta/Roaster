using BlazorMonaco.Editor;
using Microsoft.JSInterop;
using EditorGlobal = BlazorMonaco.Editor.Global;

namespace Roaster.Editor.Services;

public static class CodeUtils
{
    public const string DefaultBoilerplate = "// Paste your code here and click Submit to get a review.\n";
    public const string DefaultLanguage    = "csharp";
    public const string DefaultTheme      = "vs-dark";

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

    public static async Task SetLanguageAsync(IJSRuntime js, StandaloneCodeEditor editor, string language)
    {
        var model = await editor.GetModel();
        await EditorGlobal.SetModelLanguage(js, model, language);
    }

    public static Task SetThemeAsync(IJSRuntime js, string theme) =>
        EditorGlobal.SetTheme(js, theme);
}

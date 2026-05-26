/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
using System.Reflection;
using System.Text.Json;
using BlazorMonaco.Editor;
using Microsoft.JSInterop;
using EditorGlobal = BlazorMonaco.Editor.Global;

namespace Roaster.Editor.Utils;

public static class ThemeFactory
{
    private record ThemeEntry(string Label, string Resource);

    private static readonly Dictionary<string, ThemeEntry> Registry = new()
    {
        ["nord-dark"] = new("Nord Dark", "Roaster.Editor.Resources.NordDark.json"),
    };

    private static readonly HashSet<string> Defined = [];
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static IEnumerable<(string Key, string Label)> CustomThemes =>
        Registry.Select(kvp => (kvp.Key, kvp.Value.Label));

    public static async Task EnsureAllAsync(IJSRuntime js)
    {
        foreach (var (key, entry) in Registry)
        {
            if (Defined.Contains(key)) continue;

            var assembly = Assembly.GetExecutingAssembly();
            await using var stream = assembly.GetManifestResourceStream(entry.Resource)!;
            var themeData = await JsonSerializer.DeserializeAsync<StandaloneThemeData>(stream, JsonOptions);
            await EditorGlobal.DefineTheme(js, key, themeData!);
            Defined.Add(key);
        }
    }
}

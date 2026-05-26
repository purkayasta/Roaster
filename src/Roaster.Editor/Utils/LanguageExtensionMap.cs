namespace Roaster.Editor.Utils;

public static class LanguageExtensionMap
{
    private static readonly Dictionary<string, string> _map = new()
    {
        ["csharp"]     = ".cs",
        ["javascript"] = ".js",
        ["typescript"] = ".ts",
        ["python"]     = ".py",
        ["java"]       = ".java",
        ["go"]         = ".go",
        ["rust"]       = ".rs",
        ["cpp"]        = ".cpp",
        ["sql"]        = ".sql",
        ["json"]       = ".json",
    };

    public static string GetExtension(string language) =>
        _map.TryGetValue(language, out var ext) ? ext : ".txt";

    public static string DefaultFileName(string language) => "main" + GetExtension(language);

    public static string SwapExtension(string fileName, string language)
    {
        var ext = GetExtension(language);
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        return baseName + ext;
    }
}

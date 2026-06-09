namespace Roaster.Editor.Models;

public sealed class EditorFile
{
    public string Id { get; } = Guid.NewGuid().ToString();
    public string FileName { get; set; } = "main.cs";
    public string Language { get; set; } = "csharp";
}

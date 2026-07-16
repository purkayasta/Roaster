using System.Reflection;

namespace Roaster.Reviewer.Prompts;

internal static class PromptLoader
{
    internal static readonly string Roaster     = Load("Roaster.Reviewer.Prompts.roaster.txt");
    internal static readonly string Replier     = Load("Roaster.Reviewer.Prompts.replier.txt");
    internal static readonly string FileMessage = Load("Roaster.Reviewer.Prompts.file-message.txt");

    private static string Load(string resourceName)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded prompt resource '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

using System.Reflection;

namespace Roaster.Reviewer.Prompts;

internal static class PromptLoader
{
    internal static readonly string Replier = Load("Roaster.Reviewer.Prompts.replier.txt");
    internal static readonly string Security = Load("Roaster.Reviewer.Prompts.security.txt");
    internal static readonly string Bug = Load("Roaster.Reviewer.Prompts.bug.txt");
    internal static readonly string Syntax = Load("Roaster.Reviewer.Prompts.syntax.txt");

    internal static string Load(string resourceName)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded prompt resource '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

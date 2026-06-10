namespace Roaster.Reviewer.Prompts;

// Language-specific guidance for each review agent. Idioms, bug sources, and
// vulnerabilities are all language-specific, so we feed each agent concrete,
// per-language checks for whichever languages actually appear in the submission.
// Keyed by agent name (matches the names in ReviewerExtensions) and language id.
internal static class LanguageGuide
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Guides =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Security"] = LoadLanguages("Security"),
            ["Bug"]      = LoadLanguages("Bug"),
            ["Syntax"]   = LoadLanguages("Syntax"),
        };

    private static IReadOnlyDictionary<string, string> LoadLanguages(string agent) =>
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["csharp"]     = PromptLoader.Load($"Roaster.Reviewer.Prompts.Languages.{agent}.csharp.txt"),
            ["javascript"] = PromptLoader.Load($"Roaster.Reviewer.Prompts.Languages.{agent}.javascript.txt"),
            ["typescript"] = PromptLoader.Load($"Roaster.Reviewer.Prompts.Languages.{agent}.typescript.txt"),
        };

    // Build a guidance block for the distinct languages in the submission, for the given
    // agent. Languages without a guide (e.g. json) contribute nothing; returns "" when the
    // agent is unknown or no language matches.
    public static string ForAgent(string agentName, IEnumerable<string> languages)
    {
        if (!Guides.TryGetValue(agentName, out var byLanguage))
            return string.Empty;

        var blocks = languages
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(byLanguage.ContainsKey)
            .Select(l => byLanguage[l]);

        var joined = string.Join("\n\n", blocks);
        return string.IsNullOrWhiteSpace(joined)
            ? string.Empty
            : $"LANGUAGE-SPECIFIC CHECKS\nApply these for the language(s) in this submission, in addition to the general guidance above.\n\n{joined}";
    }
}

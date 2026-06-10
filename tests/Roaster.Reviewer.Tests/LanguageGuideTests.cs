/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
using FluentAssertions;
using Roaster.Reviewer.Prompts;

namespace Roaster.Reviewer.Tests;

public sealed class LanguageGuideTests
{
    // First-line headings that uniquely identify each embedded guide file.
    private const string Header = "LANGUAGE-SPECIFIC CHECKS";
    private const string CSharpBug = "C# — common bug sources";
    private const string JavaScriptBug = "JavaScript — common bug sources";
    private const string CSharpSecurity = "C# — common security issues";
    private const string CSharpQuality = "C# — quality checks";

    [Fact]
    public void UnknownAgent_ReturnsEmpty()
    {
        LanguageGuide.ForAgent("Nonsense", ["csharp"]).Should().BeEmpty();
    }

    [Fact]
    public void NoLanguages_ReturnsEmpty()
    {
        LanguageGuide.ForAgent("Bug", []).Should().BeEmpty();
    }

    [Fact]
    public void UnknownLanguage_ReturnsEmpty()
    {
        LanguageGuide.ForAgent("Bug", ["ruby"]).Should().BeEmpty();
    }

    [Fact]
    public void JsonLanguage_ReturnsEmpty()
    {
        LanguageGuide.ForAgent("Bug", ["json"]).Should().BeEmpty();
    }

    [Fact]
    public void MatchedLanguage_IncludesSharedHeader()
    {
        LanguageGuide.ForAgent("Bug", ["csharp"]).Should().Contain(Header);
    }

    [Fact]
    public void BugAgent_CSharp_IncludesBugGuide()
    {
        LanguageGuide.ForAgent("Bug", ["csharp"]).Should().Contain(CSharpBug);
    }

    [Fact]
    public void SecurityAgent_CSharp_IncludesSecurityGuide()
    {
        LanguageGuide.ForAgent("Security", ["csharp"]).Should().Contain(CSharpSecurity);
    }

    [Fact]
    public void SyntaxAgent_CSharp_IncludesQualityGuide()
    {
        LanguageGuide.ForAgent("Syntax", ["csharp"]).Should().Contain(CSharpQuality);
    }

    [Fact]
    public void BugAgent_DoesNotLeakSecurityGuide()
    {
        LanguageGuide.ForAgent("Bug", ["csharp"]).Should().NotContain(CSharpSecurity);
    }

    [Fact]
    public void MultipleLanguages_IncludesEach()
    {
        var guide = LanguageGuide.ForAgent("Bug", ["csharp", "javascript"]);
        guide.Should().Contain(CSharpBug).And.Contain(JavaScriptBug);
    }

    [Fact]
    public void DuplicateLanguage_IncludedOnce()
    {
        var guide = LanguageGuide.ForAgent("Bug", ["csharp", "csharp"]);
        CountOccurrences(guide, CSharpBug).Should().Be(1);
    }

    [Fact]
    public void LanguageMatch_IsCaseInsensitive()
    {
        LanguageGuide.ForAgent("Bug", ["CSharp"]).Should().Contain(CSharpBug);
    }

    [Fact]
    public void AgentMatch_IsCaseInsensitive()
    {
        LanguageGuide.ForAgent("bug", ["csharp"]).Should().Contain(CSharpBug);
    }

    [Fact]
    public void UnknownLanguageAmongKnown_IncludesOnlyKnown()
    {
        LanguageGuide.ForAgent("Bug", ["csharp", "ruby"]).Should().Contain(CSharpBug);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }
}

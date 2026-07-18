/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Roaster.Reviewer.Models;
using Roaster.Reviewer.Services;

namespace Roaster.Reviewer.Tests;

public sealed class ReviewResponseParserTests
{
    private static readonly NullLogger _logger = NullLogger.Instance;

    [Fact]
    public void ValidJson_ParsedDirectly()
    {
        var raw = """{"summary":"Looks good.","comments":[{"lineNumber":3,"comment":"Unused var.","severity":10}]}""";
        var result = ReviewResponseParser.Parse(raw, _logger);

        result.Status.Should().Be(ReviewStatus.Success);
        result.LineComments.Should().HaveCount(1);
        result.LineComments[0].LineNumber.Should().Be(3);
        result.LineComments[0].Comment.Should().Be("Unused var.");
        result.LineComments[0].Severity.Should().Be(10);
    }

    [Fact]
    public void JsonInMarkdownFence_StrippedAndParsed()
    {
        var raw = "```json\n{\"summary\":\"ok\",\"comments\":[]}\n```";
        var result = ReviewResponseParser.Parse(raw, _logger);

        result.Status.Should().Be(ReviewStatus.Success);
        result.LineComments.Should().BeEmpty();
    }

    [Fact]
    public void JsonPrecededByProse_ExtractedAndParsed()
    {
        var raw = "Here is my review:\n{\"summary\":\"nice\",\"comments\":[]}";
        var result = ReviewResponseParser.Parse(raw, _logger);

        result.Status.Should().Be(ReviewStatus.Success);
    }

    [Fact]
    public void MalformedJson_ReturnsFailedResult()
    {
        var result = ReviewResponseParser.Parse("not json at all", _logger);

        result.Status.Should().Be(ReviewStatus.Failed);
        result.LineComments.Should().BeEmpty();
    }

    [Fact]
    public void EmptyResponse_ReturnsFailedResult()
    {
        var result = ReviewResponseParser.Parse(string.Empty, _logger);

        result.Status.Should().Be(ReviewStatus.Failed);
        result.LineComments.Should().BeEmpty();
    }

    [Fact]
    public void SeverityBrutal_MappedCorrectly()
    {
        var raw = """{"summary":"oops","comments":[{"lineNumber":1,"comment":"sql injection","severity":30}]}""";
        var result = ReviewResponseParser.Parse(raw, _logger);

        result.LineComments[0].Severity.Should().Be(30);
    }

    [Fact]
    public void RawNewlineInsideStringValue_RepairedAndParsed()
    {
        var raw = "{\"summary\":\"ok\",\"comments\":[{\"lineNumber\":2,\"comment\":\"Line one\nLine two\",\"severity\":10}]}";
        var result = ReviewResponseParser.Parse(raw, _logger);

        result.Status.Should().Be(ReviewStatus.Success);
        result.LineComments[0].Comment.Should().Be("Line one\nLine two");
    }

    [Fact]
    public void CommentWithLineNumberZero_ExcludedFromResult()
    {
        var raw = """{"summary":"s","comments":[{"lineNumber":0,"comment":"bad","severity":20},{"lineNumber":5,"comment":"ok","severity":10}]}""";
        var result = ReviewResponseParser.Parse(raw, _logger);

        result.LineComments.Should().HaveCount(1);
        result.LineComments[0].LineNumber.Should().Be(5);
    }
}

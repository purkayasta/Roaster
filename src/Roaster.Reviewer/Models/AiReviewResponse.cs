/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
using System.Text.Json.Serialization;

namespace Roaster.Reviewer.Models;

internal record AiReviewResponse(
    [property: JsonPropertyName("summary")]  string? Summary,
    [property: JsonPropertyName("comments")] List<AiReviewComment>? Comments);

internal record AiReviewComment(
    [property: JsonPropertyName("lineNumber")] int LineNumber,
    [property: JsonPropertyName("comment")]    string? Comment,
    [property: JsonPropertyName("severity")]   int Severity = 20);

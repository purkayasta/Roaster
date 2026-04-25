# Phase 5 — Roaster.Reviewer

## Pre-checklist

- [ ] Add `Roaster.Reviewer` project to `Roaster.slnx` under solution folder `Backend`

---

## Overview

A class library that contains all AI inference logic. Not a deployable service — it is referenced directly by `Roaster.Consumer`. This keeps the AI concern cleanly separated at the code level without HTTP communication overhead. If independent scaling of the AI layer is needed in the future, this library can be promoted to its own HTTP service.

Uses **Microsoft Semantic Kernel** with an **Ollama** backend for local model inference.

**SDK:** `Microsoft.NET.Sdk` (net10.0, class library)

**NuGet packages:**
```xml
<PackageReference Include="Microsoft.SemanticKernel" Version="1.22.0" />
<PackageReference Include="Microsoft.SemanticKernel.Connectors.Ollama" Version="1.22.0-alpha" />
```

> **Stable fallback:** `Microsoft.SemanticKernel.Connectors.OpenAI` pointed at `http://ollama:11434/v1` (Ollama's OpenAI-compatible endpoint, supported since Ollama 0.1.24). The entire service is abstracted behind `IAiReviewService` — swapping connectors is a single constructor line change.

**Project references:** none (pure library, no dependency on Data or Contracts)

---

## Folder Structure

```
src/Roaster.Reviewer/
  Abstractions/
    IAiReviewService.cs
    AiReviewResult.cs
    AiLineComment.cs
    AiChatMessage.cs
  Options/
    OllamaOptions.cs
  Services/
    SemanticKernelAiReviewService.cs
```

---

## Abstractions

### IAiReviewService.cs

```csharp
public interface IAiReviewService
{
    Task<AiReviewResult> ReviewCodeAsync(
        string code,
        string language,
        CancellationToken ct = default);

    Task<string> ReplyToThreadAsync(
        string code,
        string originalComment,
        IList<AiChatMessage> history,
        string userMessage,
        CancellationToken ct = default);
}
```

### AiReviewResult.cs

```csharp
public record AiReviewResult(string? Summary, List<AiLineComment> LineComments);
```

### AiLineComment.cs

```csharp
public record AiLineComment(int LineNumber, string? Comment, int Severity);
// Severity matches ReviewSeverity enum values: 10 = Gentle, 20 = Medium, 30 = Brutal
```

### AiChatMessage.cs

```csharp
public record AiChatMessage(string Role, string Content);
// Role: "user" | "assistant"
```

---

## Options (`Options/OllamaOptions.cs`)

Configured via `appsettings.json` in `Roaster.Consumer`.

```csharp
public class OllamaOptions
{
    public required string Endpoint { get; init; }  // e.g. "http://ollama:11434"
    public required string ModelId  { get; init; }  // e.g. "deepseek-coder:6.7b"
}
```

**Recommended models:**
- `deepseek-coder:6.7b` — best JSON compliance for structured output, ~6GB VRAM (q4 quantized)
- `codellama:13b` — broader language support, ~8GB VRAM (q4 quantized)

---

## SemanticKernelAiReviewService.cs

### System Prompt — Code Review

```
You are an expert code reviewer. Return ONLY a valid JSON object with no markdown, no prose outside the JSON:
{
  "summary": "1-2 sentence overall assessment.",
  "comments": [
    { "lineNumber": 5, "comment": "Explanation of the issue.", "severity": 20 }
  ]
}
Severity values: 10 = Gentle (style suggestion), 20 = Medium (correctness or performance issue), 30 = Brutal (security vulnerability or data loss risk).
If there are no issues, return an empty comments array.
```

### System Prompt — Thread Reply

```
You are an expert code reviewer continuing a conversation with a developer about a specific line of code.
You will receive: the original code, your original comment about a specific line, the conversation so far, and the developer's latest message.
Reply helpfully and concisely in plain text. No JSON. No markdown headers. Reference specific lines or code snippets when useful. Build on the conversation — do not repeat your original comment verbatim.
```

---

### ReviewCodeAsync implementation

1. Build `ChatHistory` with the code review system prompt
2. Add user turn: language + code fenced block
3. Call `IChatCompletionService.GetChatMessageContentAsync` with low temperature (0.2 — deterministic)
4. Call `ParseReviewResponse(response.Content)` to get `AiReviewResult`

### ReplyToThreadAsync implementation

1. Build `ChatHistory` with the thread reply system prompt
2. Add a priming exchange: inject original code + original comment as the first user turn, AI acknowledges with a fixed assistant turn
3. Replay all existing messages from `history` (`AiChatMessage.Role` → `AddUserMessage` or `AddAssistantMessage`)
4. Add current `userMessage` as a user turn
5. Call `GetChatMessageContentAsync` with temperature 0.5
6. Return `response.Content?.Trim()`

---

### ParseReviewResponse

Handles models that sometimes wrap JSON in markdown fences:

1. Try direct `JsonDocument.Parse(raw)`
2. If fails, strip ` ```json ... ``` ` via regex and retry
3. If still fails, find first `{` and last `}` and extract that substring, retry
4. If `JsonException` on all attempts: log the raw response (truncated to 500 chars) and return `new AiReviewResult(null, [])` — graceful empty result, no retry triggered

```csharp
private AiReviewResult ParseReviewResponse(string raw)
{
    var json = ExtractJson(raw);
    try
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var summary = root.TryGetProperty("summary", out var s) ? s.GetString() : null;
        var comments = new List<AiLineComment>();
        if (root.TryGetProperty("comments", out var arr))
        {
            foreach (var item in arr.EnumerateArray())
            {
                var line     = item.TryGetProperty("lineNumber", out var ln) ? ln.GetInt32() : 0;
                var comment  = item.TryGetProperty("comment", out var c) ? c.GetString() : null;
                var severity = item.TryGetProperty("severity", out var sv) ? sv.GetInt32() : 20;
                if (line > 0) comments.Add(new AiLineComment(line, comment, severity));
            }
        }
        return new AiReviewResult(summary, comments);
    }
    catch (JsonException ex)
    {
        _logger.LogWarning(ex, "Failed to parse AI response. Raw (truncated): {Raw}",
            raw[..Math.Min(raw.Length, 500)]);
        return new AiReviewResult(null, []);
    }
}

private static string ExtractJson(string raw)
{
    var match = Regex.Match(raw, @"```(?:json)?\s*([\s\S]*?)```", RegexOptions.Multiline);
    if (match.Success) return match.Groups[1].Value.Trim();
    var start = raw.IndexOf('{');
    var end   = raw.LastIndexOf('}');
    return start >= 0 && end > start ? raw[start..(end + 1)] : raw;
}
```
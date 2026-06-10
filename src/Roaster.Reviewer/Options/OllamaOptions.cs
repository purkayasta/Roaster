/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
namespace Roaster.Reviewer.Options;

public sealed class OllamaOptions
{
    public required string Endpoint { get; init; }  // e.g. "http://ollama:11434"
    public required string ModelId  { get; init; }  // e.g. "deepseek-coder:6.7b"

    // Provider-neutral sampling settings (MEAI naming) so they carry over unchanged
    // on the SK→MAF swap. Materialised into the connector-specific settings type in
    // ReviewerExtensions — the one place that changes during migration.
    public double Temperature      { get; init; } = 0.2;   // low: grounded findings (minimises invented issues), a little room for the roast
    public int?   MaxOutputTokens  { get; init; }          // null = uncapped (avoids truncating the JSON response)
}

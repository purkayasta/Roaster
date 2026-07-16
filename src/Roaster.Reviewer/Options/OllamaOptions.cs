/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
namespace Roaster.Reviewer.Options;

public sealed class OllamaOptions
{
    public required string Endpoint      { get; init; }  // e.g. "http://ollama:11434"
    public required string ModelId       { get; init; }  // e.g. "deepseek-coder:6.7b"
    public required int    ContextLength { get; init; }   // num_ctx — varies per model, must be set explicitly
}

namespace Roaster.Reviewer.Options;

public class OllamaOptions
{
    public required string Endpoint { get; init; }  // e.g. "http://ollama:11434"
    public required string ModelId  { get; init; }  // e.g. "deepseek-coder:6.7b"
}

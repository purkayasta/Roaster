namespace Roaster.Data.Models;

public record CodePayload
{
    public required string Content  { get; init; }
    public required string Language { get; init; }
}

namespace Roaster.Data.Models;

public record ReviewLineComment
{
    public int LineNumber { get; init; }
    public string? Comment { get; init; }
    public ReviewSeverity Severity { get; init; }
}

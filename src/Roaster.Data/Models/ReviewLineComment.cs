namespace Roaster.Data.Models;

public sealed record ReviewLineComment
{
    public int LineNumber { get; init; }
    public string? Comment { get; init; }
    public ReviewSeverity Severity { get; init; }
}

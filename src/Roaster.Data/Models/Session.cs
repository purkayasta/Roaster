namespace Roaster.Data.Models;

public sealed class Session
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public DateTime CreatedAt { get; init; }

    public Submission? Submission { get; set; }
}

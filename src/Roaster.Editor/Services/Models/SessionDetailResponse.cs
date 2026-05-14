namespace Roaster.Editor.Services.Models;

public record SessionDetailResponse(
    Guid Id,
    string Name,
    DateTime CreatedAt,
    string? CodeContent,
    string? Language,
    string? Status);

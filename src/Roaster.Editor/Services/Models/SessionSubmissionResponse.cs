namespace Roaster.Editor.Services.Models;

public record SessionSubmissionResponse(
    long? SubmissionId,
    Guid SessionId,
    string SessionName,
    string? Language,
    string? Status,
    DateTime SessionCreatedAt);

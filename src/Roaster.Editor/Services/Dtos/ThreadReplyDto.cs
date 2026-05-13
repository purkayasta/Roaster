namespace Roaster.Editor.Services.Dtos;

public record ThreadReplyDto(long Id, string? Content, bool IsAiReply, DateTime CreatedAt);

/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
namespace Roaster.Editor.Services.Models;

public record ReviewResponse(long Id, long SubmissionId, string? Summary, DateTime CreatedAt, List<ReviewCommentResponse> Comments);
public record ReviewCommentResponse(int LineNumber, string? Comment, string Severity, long? ThreadId, bool IsResolved);
public record ThreadReplyResponse(long Id, string? Content, bool IsAiReply, DateTime CreatedAt);
public record ThreadCreatedResponse(long ThreadId, long ReplyId);

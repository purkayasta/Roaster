using Roaster.Editor.Services.Dtos;

namespace Roaster.Editor.Services;

public interface IRoastApiService
{
    Task<List<RoastComment>> SubmitCodeAsync(SubmissionRequest request, CancellationToken ct = default);
    Task<bool> ReplyToThreadAsync(long submissionId, long reviewId, int lineNumber, string content, CancellationToken ct = default);
    Task<List<ThreadReplyDto>> GetThreadRepliesAsync(long threadId, CancellationToken ct = default);
}

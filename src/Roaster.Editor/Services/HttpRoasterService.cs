using Roaster.Editor.Services.Dtos;
using Roaster.Editor.Services.Http;

namespace Roaster.Editor.Services;

public class HttpRoasterService(SubmissionService submissions, ReviewService reviews, ThreadService threads) : IRoastApiService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);

    public async Task<List<RoastComment>> SubmitCodeAsync(SubmissionRequest request, CancellationToken ct = default)
    {
        var submission = await submissions.CreateAsync(request.Code, request.Language, ct);

        var deadline = DateTime.UtcNow.Add(Timeout);
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(PollingInterval, ct);

            var current = await submissions.GetAsync(submission.Id, ct);
            if (current?.Status == "Failed")
                throw new InvalidOperationException("Code review failed on the server.");

            var review = await reviews.GetAsync(submission.Id, ct);
            if (review is not null)
                return MapComments(submission.Id, review);
        }

        throw new TimeoutException("Review did not complete within the expected time.");
    }

    public async Task<bool> ReplyToThreadAsync(
        long submissionId, long reviewId, int lineNumber, string content, CancellationToken ct = default)
    {
        try
        {
            await threads.CreateReplyAsync(submissionId, reviewId, lineNumber, content, ct);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public Task<List<ThreadReplyDto>> GetThreadRepliesAsync(long threadId, CancellationToken ct = default)
        => threads.GetRepliesAsync(threadId, ct);

    private static List<RoastComment> MapComments(long submissionId, ReviewResponseDto review) =>
        review.Comments.Select(c => new RoastComment
        {
            SubmissionId = submissionId,
            ReviewId     = review.Id,
            LineNumber   = c.LineNumber,
            CommentText  = c.Comment ?? string.Empty,
            Severity     = c.Severity,
            ThreadId     = c.ThreadId?.ToString()
        }).ToList();
}

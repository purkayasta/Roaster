using Roaster.Editor.Services.Dtos;

namespace Roaster.Editor.Services;

public class MockRoastApiService : IRoastApiService
{
    public async Task<List<RoastComment>> SubmitCodeAsync(SubmissionRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            return [];

        await Task.Delay(2000, ct);

        return
        [
            new RoastComment
            {
                SubmissionId = 1,
                ReviewId     = 1,
                LineNumber   = 3,
                CommentText  = "Ah, I see you're declaring a `UserService`. Does this service actually do anything useful, or is it just a placeholder for your ambitions?",
                Severity     = "Medium",
                ThreadId     = "roast-3"
            },
            new RoastComment
            {
                SubmissionId = 1,
                ReviewId     = 1,
                LineNumber   = 5,
                CommentText  = "Wait, you're using `IRepository`? How delightfully abstract of you. I'm sure the implementation definitely isn't just returning `null` for everything.",
                Severity     = "Brutal",
                ThreadId     = "roast-5"
            }
        ];
    }

    public async Task<bool> ReplyToThreadAsync(
        long submissionId, long reviewId, int lineNumber, string content, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(content)) return false;
        await Task.Delay(1000, ct);
        return true;
    }

    public Task<List<ThreadReplyDto>> GetThreadRepliesAsync(long threadId, CancellationToken ct = default)
        => Task.FromResult(new List<ThreadReplyDto>());
}

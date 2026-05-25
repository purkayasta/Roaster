using Microsoft.EntityFrameworkCore;
using Roaster.Contracts.Dtos;
using Roaster.Data.Context;
using Roaster.Data.Models;

namespace Roaster.Api.Services;

public sealed class ReviewService(RoasterDbContext db, ILogger<ReviewService> logger)
{
    public async Task<ServiceResult<ReviewDto>> GetBySubmissionIdAsync(long submissionId, CancellationToken ct)
    {
        var submission = await db.Submissions
            .Include(s => s.Review).ThenInclude(r => r!.Threads)
            .FirstOrDefaultAsync(s => s.Id == submissionId, ct);

        if (submission is null) return ServiceResult<ReviewDto>.NotFound();

        if (submission.Status != SubmissionStatus.Completed || submission.Review is null)
        {
            logger.LogInformation("Review not ready for submission {Id}, status is {Status}", submissionId, submission.Status);
            return ServiceResult<ReviewDto>.NoContent();
        }

        var review = submission.Review;
        logger.LogInformation("Review {ReviewId} found for submission {Id}, returning {Count} comments", review.Id, submissionId, review.Comments.Count);

        return ServiceResult<ReviewDto>.Ok(new ReviewDto(
            review.Id,
            review.SubmissionId,
            review.Summary,
            review.CreatedAt,
            review.Comments.Select(c =>
            {
                var thread = review.Threads.FirstOrDefault(t => t.LineNumber == c.LineNumber);
                return new ReviewCommentDto(c.LineNumber, c.Comment, c.Severity.ToString(), thread?.Id, thread?.Status == ThreadStatus.Resolved);
            }).ToList()));
    }
}

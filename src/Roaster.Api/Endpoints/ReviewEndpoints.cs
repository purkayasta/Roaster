using Microsoft.EntityFrameworkCore;
using Roaster.Contracts.Dtos;
using Roaster.Data.Context;
using Roaster.Data.Models;

namespace Roaster.Api.Endpoints;

public static class ReviewEndpoints
{
    public static void MapReviewEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/submissions/{id:long}/review", async (long id, RoasterDbContext db, ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("Reviews");
            logger.LogInformation("Fetching review for submission {Id}", id);

            var submission = await db.Submissions
                .Include(s => s.Review)
                    .ThenInclude(r => r!.Threads)
                .FirstOrDefaultAsync(s => s.Id == id, ct);

            if (submission is null)
            {
                logger.LogWarning("Submission {Id} not found when fetching review", id);
                return Results.NotFound();
            }

            if (submission.Status != SubmissionStatus.Completed || submission.Review is null)
            {
                logger.LogInformation("Review not ready for submission {Id}, status is {Status}", id, submission.Status);
                return Results.NoContent();
            }

            var review = submission.Review;
            logger.LogInformation("Review {ReviewId} found for submission {Id}, returning {CommentCount} comments", review.Id, id, review.Comments.Count);

            return Results.Ok(new ReviewDto(
                review.Id,
                review.SubmissionId,
                review.Summary,
                review.CreatedAt,
                review.Comments.Select(c => new ReviewCommentDto(
                    c.LineNumber,
                    c.Comment,
                    c.Severity.ToString(),
                    review.Threads.FirstOrDefault(t => t.LineNumber == c.LineNumber)?.Id
                )).ToList()));
        }).WithName("GetReview");
    }
}

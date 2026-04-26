using Microsoft.EntityFrameworkCore;
using Roaster.Contracts.Dtos;
using Roaster.Data.Context;
using Roaster.Data.Models;

namespace Roaster.Api.Endpoints;

public static class ReviewEndpoints
{
    public static void MapReviewEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/submissions/{id:long}/review", async (long id, RoasterDbContext db, CancellationToken ct) =>
        {
            var submission = await db.Submissions
                .Include(s => s.Review)
                    .ThenInclude(r => r!.Threads)
                .FirstOrDefaultAsync(s => s.Id == id, ct);

            if (submission is null)
                return Results.NotFound();

            if (submission.Status != SubmissionStatus.Completed || submission.Review is null)
                return Results.NoContent();

            var review = submission.Review;

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

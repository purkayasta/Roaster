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
            var threadsByLine = review.Threads.ToDictionary(t => t.LineNumber, t => t.Id);

            var comments = review.Comments.Select(c => new ReviewCommentDto(
                c.LineNumber,
                c.Comment,
                c.Severity.ToString(),
                threadsByLine.TryGetValue(c.LineNumber, out var tid) ? tid : null
            )).ToList();

            var dto = new ReviewDto(review.Id, review.SubmissionId, review.Summary, review.CreatedAt, comments);
            return Results.Ok(dto);
        }).WithName("GetReview");
    }
}

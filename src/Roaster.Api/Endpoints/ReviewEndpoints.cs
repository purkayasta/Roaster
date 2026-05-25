using Roaster.Api.Services;

namespace Roaster.Api.Endpoints;

public static class ReviewEndpoints
{
    public static void MapReviewEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/submissions/{id:long}/review", async (long id, ReviewService service, CancellationToken ct) =>
            await service.GetBySubmissionIdAsync(id, ct)
        ).WithName("GetReview");
    }
}

using Roaster.Api.Models;
using Roaster.Api.Services;

namespace Roaster.Api.Endpoints;

public static class ThreadEndpoints
{
    public static void MapThreadEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/submissions/{submissionId:long}/reviews/{reviewId:long}/lines/{lineNumber:int}/threads",
            async (long submissionId, long reviewId, int lineNumber, ReplyRequest req, ThreadService service, CancellationToken ct) =>
            {
                var result = await service.AddReplyAsync(submissionId, reviewId, lineNumber, req, ct);
                if (result.Status != ResultStatus.Ok) return (IResult)result;
                return Results.Accepted(null, result.Data);
            }).WithName("CreateThread");

        app.MapPatch("/api/submissions/{submissionId:long}/reviews/{reviewId:long}/lines/{lineNumber:int}/resolve",
            async (long submissionId, long reviewId, int lineNumber, ThreadService service, CancellationToken ct) =>
                await service.ResolveAsync(submissionId, reviewId, lineNumber, ct)
        ).WithName("ResolveThread");

        app.MapGet("/api/threads/{threadId:long}/replies", async (long threadId, ThreadService service, CancellationToken ct) =>
            await service.GetRepliesAsync(threadId, ct)
        ).WithName("GetReplies");
    }
}

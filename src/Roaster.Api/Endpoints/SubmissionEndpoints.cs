using Roaster.Api.Models;
using Roaster.Api.Services;

namespace Roaster.Api.Endpoints;

public static class SubmissionEndpoints
{
    public static void MapSubmissionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/submissions", async (SubmissionRequest req, SubmissionService service, CancellationToken ct) =>
        {
            var result = await service.CreateAsync(req, ct);
            if (result.Status != ResultStatus.Ok) return (IResult)result;
            return Results.AcceptedAtRoute("GetSubmission", new { id = result.Data!.Id }, result.Data);
        }).WithName("CreateSubmission");

        app.MapGet("/api/submissions/{id:long}", async (long id, SubmissionService service, CancellationToken ct) =>
            await service.GetByIdAsync(id, ct)
        ).WithName("GetSubmission");
    }
}

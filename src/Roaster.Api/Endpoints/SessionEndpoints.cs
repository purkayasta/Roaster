using Roaster.Api.Models;
using Roaster.Api.Services;

namespace Roaster.Api.Endpoints;

public static class SessionEndpoints
{
    public static void MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/sessions", async (CreateSessionRequest req, SessionService service, CancellationToken ct) =>
        {
            var result = await service.CreateAsync(req, ct);
            return Results.Created($"/api/sessions/{result.Data!.Id}", result.Data);
        }).WithName("CreateSession");

        app.MapGet("/api/sessions", async (int page, int pageSize, SessionService service, CancellationToken ct) =>
            await service.GetAllAsync(page, pageSize, ct)
        ).WithName("ListSessions");

        app.MapGet("/api/sessions/{id:guid}", async (Guid id, SessionService service, CancellationToken ct) =>
            await service.GetByIdAsync(id, ct)
        ).WithName("GetSession");

        app.MapDelete("/api/sessions/{id:guid}", async (Guid id, SessionService service, CancellationToken ct) =>
            await service.DeleteAsync(id, ct)
        ).WithName("DeleteSession");
    }
}

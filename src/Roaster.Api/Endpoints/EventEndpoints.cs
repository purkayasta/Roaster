using Roaster.Api.Services;

namespace Roaster.Api.Endpoints;

public static class EventEndpoints
{
    public static void MapEventEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/events", async (int page, int pageSize, Guid? sessionId, EventService service, CancellationToken ct) =>
            await service.GetAllAsync(page, pageSize, sessionId, ct)
        ).WithName("ListEvents");
    }
}

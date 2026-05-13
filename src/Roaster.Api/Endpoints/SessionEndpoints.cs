using Roaster.Api.Models;
using Roaster.Contracts.Dtos;
using Roaster.Data.Context;
using Roaster.Data.Models;

namespace Roaster.Api.Endpoints;

public static class SessionEndpoints
{
    public static void MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/sessions", async (CreateSessionRequest req, RoasterDbContext db,
            ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("Sessions");
            logger.LogInformation("Creating session with name {Name}", req.Name);

            var session = new Session
            {
                Name      = req.Name,
                CreatedAt = DateTime.UtcNow
            };

            db.Sessions.Add(session);
            await db.SaveChangesAsync(ct);

            logger.LogInformation("Session {Id} created", session.Id);
            return Results.Created($"/api/sessions/{session.Id}",
                new SessionDto(session.Id, session.Name, session.CreatedAt));
        }).WithName("CreateSession");
    }
}

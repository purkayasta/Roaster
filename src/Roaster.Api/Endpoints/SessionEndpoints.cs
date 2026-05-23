using Microsoft.EntityFrameworkCore;
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

        app.MapGet("/api/sessions", async (int page, int pageSize, RoasterDbContext db,
            ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("Sessions");
            page     = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 20 : pageSize;
            logger.LogInformation("Listing sessions page {Page}, pageSize {PageSize}", page, pageSize);

            var sessions = await db.Sessions
                .Include(s => s.Submission)
                .Where(s => s.IsActive)
                .OrderByDescending(s => s.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            var results = sessions.Select(s => new SessionSubmissionDto(
                s.Submission?.Id,
                s.Id,
                s.Name,
                s.Submission?.Payload.Language,
                s.Submission?.Status.ToString(),
                s.CreatedAt)).ToList();

            logger.LogInformation("Returning {Count} sessions", results.Count);
            return Results.Ok(results);
        }).WithName("ListSessions");

        app.MapGet("/api/sessions/{id:guid}", async (Guid id, RoasterDbContext db,
            ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("Sessions");
            var session = await db.Sessions
                .Include(s => s.Submission)
                .FirstOrDefaultAsync(s => s.Id == id, ct);

            if (session is null)
            {
                logger.LogWarning("Session {Id} not found", id);
                return Results.NotFound();
            }

            logger.LogInformation("Session {Id} fetched", id);
            return Results.Ok(new SessionDetailDto(
                session.Id,
                session.Name,
                session.CreatedAt,
                session.Submission?.Payload.Content,
                session.Submission?.Payload.Language,
                session.Submission?.Status.ToString(),
                session.Submission?.Id));
        }).WithName("GetSession");

        app.MapDelete("/api/sessions/{id:guid}", async (Guid id, RoasterDbContext db,
            ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("Sessions");
            var session = await db.Sessions.FindAsync([id], ct);
            if (session is null)
            {
                logger.LogWarning("Session {Id} not found for soft delete", id);
                return Results.NotFound();
            }

            session.IsActive = false;
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Session {Id} soft deleted", id);
            return Results.NoContent();
        }).WithName("DeleteSession");
    }
}

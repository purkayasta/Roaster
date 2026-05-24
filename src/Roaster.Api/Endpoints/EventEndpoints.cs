using Microsoft.EntityFrameworkCore;
using Roaster.Contracts.Dtos;
using Roaster.Data.Context;
using Roaster.Data.Models;

namespace Roaster.Api.Endpoints;

public static class EventEndpoints
{
    public static void MapEventEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/events", async (int page, int pageSize, Guid? sessionId, RoasterDbContext db,
            ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("Events");
            page     = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 30 : pageSize;
            logger.LogInformation("Listing events page {Page}, pageSize {PageSize}, sessionId {SessionId}",
                page, pageSize, sessionId);

            if (sessionId.HasValue && !await db.Sessions.AnyAsync(s => s.Id == sessionId.Value, ct))
            {
                logger.LogWarning("Session {SessionId} not found", sessionId);
                return Results.NotFound();
            }

            var results = await db.SubmissionEvents
                .Where(e => sessionId == null || e.SessionId == sessionId)
                .OrderByDescending(e => e.OccurredAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(e => new EventLogDto(
                    e.Id,
                    e.SubmissionId,
                    e.SessionId,
                    e.EventType.ToString(),
                    e.OccurredAt,
                    e.Submission.Payload.Language,
                    e.Metadata,
                    e.EventType == SubmissionEventType.ReviewCompleted && e.Submission.Metrics != null ? e.Submission.Metrics.ModelId : null,
                    e.EventType == SubmissionEventType.ReviewCompleted && e.Submission.Metrics != null ? (int?)e.Submission.Metrics.QueueWaitMs : null,
                    e.EventType == SubmissionEventType.ReviewCompleted && e.Submission.Metrics != null ? (int?)e.Submission.Metrics.ReviewDurationMs : null,
                    e.EventType == SubmissionEventType.ReviewCompleted && e.Submission.Metrics != null ? (int?)e.Submission.Metrics.TotalDurationMs : null
                ))
                .ToListAsync(ct);

            logger.LogInformation("Returning {Count} events", results.Count);
            return Results.Ok(results);
        }).WithName("ListEvents");
    }
}

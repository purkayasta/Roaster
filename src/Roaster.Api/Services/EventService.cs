using Microsoft.EntityFrameworkCore;
using Roaster.Contracts.Dtos;
using Roaster.Data.Context;
using Roaster.Data.Models;

namespace Roaster.Api.Services;

public sealed class EventService(RoasterDbContext db, ILogger<EventService> logger)
{
    public async Task<ServiceResult<List<EventLogDto>>> GetAllAsync(int page, int pageSize, Guid? sessionId, CancellationToken ct)
    {
        page     = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 30 : pageSize;

        if (sessionId.HasValue && !await db.Sessions.AnyAsync(s => s.Id == sessionId.Value, ct))
            return ServiceResult<List<EventLogDto>>.NotFound();

        var events = await db.Events
            .Include(e => e.Submission)
                .ThenInclude(s => s.Review!)
                    .ThenInclude(r => r.Metrics)
            .Where(e => sessionId == null || e.SessionId == sessionId)
            .OrderByDescending(e => e.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var results = events.Select(e =>
        {
            var metrics = e.EventType == EventType.ReviewCompleted ? e.Submission.Review?.Metrics : null;
            return new EventLogDto(
                e.Id,
                e.SubmissionId,
                e.SessionId,
                e.EventType.ToString(),
                e.OccurredAt,
                e.Submission.Payloads.FirstOrDefault()?.Language,
                e.Metadata,
                metrics?.ModelId,
                metrics?.QueueWaitMs,
                metrics?.ReviewDurationMs,
                metrics?.TotalDurationMs);
        }).ToList();

        logger.LogInformation("Returning {Count} events", results.Count);
        return ServiceResult<List<EventLogDto>>.Ok(results);
    }
}

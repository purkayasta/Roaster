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

        var results = await db.Events
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
                e.EventType == EventType.ReviewCompleted && e.Submission.Review != null && e.Submission.Review.Metrics != null ? e.Submission.Review.Metrics.ModelId : null,
                e.EventType == EventType.ReviewCompleted && e.Submission.Review != null && e.Submission.Review.Metrics != null ? (int?)e.Submission.Review.Metrics.QueueWaitMs : null,
                e.EventType == EventType.ReviewCompleted && e.Submission.Review != null && e.Submission.Review.Metrics != null ? (int?)e.Submission.Review.Metrics.ReviewDurationMs : null,
                e.EventType == EventType.ReviewCompleted && e.Submission.Review != null && e.Submission.Review.Metrics != null ? (int?)e.Submission.Review.Metrics.TotalDurationMs : null
            ))
            .ToListAsync(ct);

        logger.LogInformation("Returning {Count} events", results.Count);
        return ServiceResult<List<EventLogDto>>.Ok(results);
    }
}

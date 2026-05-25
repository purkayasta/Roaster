using Microsoft.EntityFrameworkCore;
using Roaster.Api.Models;
using Roaster.Contracts.Dtos;
using Roaster.Data.Context;
using Roaster.Data.Models;

namespace Roaster.Api.Services;

public sealed class SessionService(RoasterDbContext db, ILogger<SessionService> logger)
{
    public async Task<ServiceResult<SessionDto>> CreateAsync(CreateSessionRequest req, CancellationToken ct)
    {
        var session = new Session { Name = req.Name, CreatedAt = DateTime.UtcNow };
        db.Sessions.Add(session);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Session {Id} created", session.Id);
        return ServiceResult<SessionDto>.Ok(new SessionDto(session.Id, session.Name, session.CreatedAt));
    }

    public async Task<ServiceResult<List<SessionSubmissionDto>>> GetAllAsync(int page, int pageSize, CancellationToken ct)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 20 : pageSize;

        var sessions = await db.Sessions
            .Include(s => s.Submissions)
                .ThenInclude(sub => sub.Review)
                    .ThenInclude(r => r!.Threads)
            .Where(s => s.IsActive)
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var results = sessions.Select(s =>
        {
            var latest = s.Submissions.MaxBy(sub => sub.Id);
            var total    = latest?.Review?.Comments.Count ?? 0;
            var resolved = latest?.Review?.Threads.Count(t => t.Status == ThreadStatus.Resolved) ?? 0;
            return new SessionSubmissionDto(latest?.Id, s.Id, s.Name, latest?.Payload.Language, latest?.Status.ToString(), s.CreatedAt, total, resolved);
        }).ToList();

        logger.LogInformation("Returning {Count} sessions", results.Count);
        return ServiceResult<List<SessionSubmissionDto>>.Ok(results);
    }

    public async Task<ServiceResult<SessionDetailDto>> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var session = await db.Sessions
            .Include(s => s.Submissions)
                .ThenInclude(sub => sub.Review)
                    .ThenInclude(r => r!.Threads)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        if (session is null) return ServiceResult<SessionDetailDto>.NotFound();

        var latest   = session.Submissions.MaxBy(s => s.Id);
        var total    = latest?.Review?.Comments.Count ?? 0;
        var resolved = latest?.Review?.Threads.Count(t => t.Status == ThreadStatus.Resolved) ?? 0;
        logger.LogInformation("Session {Id} fetched", id);
        return ServiceResult<SessionDetailDto>.Ok(new SessionDetailDto(
            session.Id, session.Name, session.CreatedAt,
            latest?.Payload.Content, latest?.Payload.Language, latest?.Status.ToString(), latest?.Id,
            total, resolved));
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        var session = await db.Sessions.FindAsync([id], ct);
        if (session is null) return ServiceResult.NotFound();

        session.IsActive = false;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Session {Id} soft deleted", id);
        return ServiceResult.NoContent();
    }
}

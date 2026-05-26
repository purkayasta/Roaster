using System.Text.RegularExpressions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Roaster.Api.Models;
using Roaster.Contracts.Dtos;
using Roaster.Contracts.Events;
using Roaster.Data.Context;
using Roaster.Data.Models;

namespace Roaster.Api.Services;

public sealed partial class SubmissionService(RoasterDbContext db, IPublishEndpoint publisher, ILogger<SubmissionService> logger)
{
    [GeneratedRegex(@"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]")]
    private static partial Regex ControlCharsRegex();
    public async Task<ServiceResult<SubmissionDto>> CreateAsync(SubmissionRequest req, CancellationToken ct)
    {
        var session = await db.Sessions.FindAsync([req.SessionId], ct);
        if (session is null) return ServiceResult<SubmissionDto>.NotFound($"Session {req.SessionId} not found.");

        var inFlight = await db.Submissions
            .AnyAsync(s => s.SessionId == req.SessionId && s.Status == SubmissionStatus.Processing, ct);
        if (inFlight) return ServiceResult<SubmissionDto>.Conflict("A review is currently in progress. Please wait before submitting again.");

        var files = req.Files
            .Select(f => new Payload
            {
                FileName = f.FileName,
                Content = ControlCharsRegex().Replace(f.Content, ""),
                Language = f.Language
            })
            .ToList();

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var submission = new Submission
        {
            SessionId = req.SessionId,
            Payloads = files,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Submissions.Add(submission);
        await db.SaveChangesAsync(ct);

        db.Events.Add(new Roaster.Data.Models.Event
        {
            SubmissionId = submission.Id,
            SessionId = req.SessionId,
            EventType = EventType.Submitted,
            CorrelationId = session.Id,
            OccurredAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await publisher.Publish(new SubmissionCreatedEvent(submission.Id, session.Id, DateTime.UtcNow), ct);
        logger.LogInformation("Submission {Id} created for session {SessionId} with {FileCount} file(s)",
            submission.Id, req.SessionId, submission.Payloads.Count);

        return ServiceResult<SubmissionDto>.Ok(ToDto(submission));
    }

    public async Task<ServiceResult<SubmissionDto>> GetByIdAsync(long id, CancellationToken ct)
    {
        var submission = await db.Submissions.FindAsync([id], ct);
        if (submission is null) return ServiceResult<SubmissionDto>.NotFound();

        logger.LogInformation("Submission {Id} found, status {Status}", submission.Id, submission.Status);
        return ServiceResult<SubmissionDto>.Ok(ToDto(submission));
    }

    private static SubmissionDto ToDto(Submission s) =>
        new(s.Id, s.Payloads.Select(f => f.FileName).ToList(), s.Status.ToString(), s.CreatedAt, s.UpdatedAt);
}

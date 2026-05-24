using System.Text.RegularExpressions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Roaster.Api.Models;
using Roaster.Contracts.Dtos;
using Roaster.Contracts.Events;
using Roaster.Data.Context;
using Roaster.Data.Models;

namespace Roaster.Api.Endpoints;

public static class SubmissionEndpoints
{
    public static void MapSubmissionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/submissions", async (SubmissionRequest req, RoasterDbContext db,
            IPublishEndpoint publisher, ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("Submissions");

            var session = await db.Sessions.FindAsync([req.SessionId], ct);
            if (session is null)
            {
                logger.LogWarning("Session {SessionId} not found", req.SessionId);
                return Results.NotFound($"Session {req.SessionId} not found.");
            }

            var correlationId = session.Id;
            var sanitizedCode = Regex.Replace(req.Code, @"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", "");

            var existing = await db.Submissions
                .Include(s => s.Review)
                .FirstOrDefaultAsync(s => s.SessionId == req.SessionId, ct);

            if (existing is not null && existing.Status == SubmissionStatus.Processing)
            {
                logger.LogWarning("Submission {Id} for session {SessionId} is currently Processing — rejecting resubmit",
                    existing.Id, req.SessionId);
                return Results.Conflict(
                    "Submission is currently being reviewed. Please wait for the review to complete before resubmitting.");
            }

            await using var tx = await db.Database.BeginTransactionAsync(ct);

            Submission submission;

            if (existing is null)
            {
                submission = new Submission
                {
                    SessionId = req.SessionId,
                    Payload   = new CodePayload { Content = sanitizedCode, Language = req.Language },
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                db.Submissions.Add(submission);
                await db.SaveChangesAsync(ct);
                logger.LogInformation("New submission {Id} created for session {SessionId}", submission.Id, req.SessionId);
            }
            else
            {
                submission = existing;

                if (existing.Review is not null)
                    db.Reviews.Remove(existing.Review);

                submission.Payload = new CodePayload { Content = sanitizedCode, Language = req.Language };
                submission.Status  = SubmissionStatus.Submitted;
                await db.SaveChangesAsync(ct);  // triggers CaptureSubmissionHistory + UpdatedAt
                logger.LogInformation("Submission {Id} updated for session {SessionId}", submission.Id, req.SessionId);
            }

            db.SubmissionEvents.Add(new SubmissionEvent
            {
                SubmissionId  = submission.Id,
                SessionId     = req.SessionId,
                EventType     = SubmissionEventType.Submitted,
                CorrelationId = correlationId,
                OccurredAt    = DateTime.UtcNow
            });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            logger.LogInformation("Submission {Id} saved, publishing CodeSubmittedEvent. CorrelationId={CorrelationId}",
                submission.Id, correlationId);

            await publisher.Publish(new CodeSubmittedEvent(submission.Id, correlationId, DateTime.UtcNow), ct);

            return Results.AcceptedAtRoute("GetSubmission", new { id = submission.Id }, ToDto(submission));
        }).WithName("CreateSubmission");

        app.MapGet("/api/submissions/{id:long}", async (long id, RoasterDbContext db, ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("Submissions");
            logger.LogInformation("Fetching submission {Id}", id);
            var submission = await db.Submissions.FindAsync([id], ct);
            if (submission is null)
            {
                logger.LogWarning("Submission {Id} not found", id);
                return Results.NotFound();
            }
            logger.LogInformation("Submission {Id} found, status {Status}", submission.Id, submission.Status);
            return Results.Ok(ToDto(submission));
        }).WithName("GetSubmission");

    }

    private static SubmissionDto ToDto(Submission s) =>
        new(s.Id, s.Payload.Language, s.Status.ToString(), s.CreatedAt, s.UpdatedAt);
}

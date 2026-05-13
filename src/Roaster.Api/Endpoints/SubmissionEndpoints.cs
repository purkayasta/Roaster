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
            IPublishEndpoint publisher, HttpContext ctx, ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("Submissions");
            var correlationId = (Guid)ctx.Items["CorrelationId"]!;
            logger.LogInformation("Received submission for language {Language}, code length {Length} chars", req.Language, req.Code.Length);

            var submission = new Submission
            {
                Payload = new CodePayload { Content = Regex.Replace(req.Code, @"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", ""), Language = req.Language },
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await using var tx = await db.Database.BeginTransactionAsync(ct);

            db.Submissions.Add(submission);
            await db.SaveChangesAsync(ct);

            db.SubmissionEvents.Add(new SubmissionEvent
            {
                SubmissionId  = submission.Id,
                EventType     = SubmissionEventType.Submitted,
                CorrelationId = correlationId,
                OccurredAt    = submission.CreatedAt
            });
            await db.SaveChangesAsync(ct);

            await tx.CommitAsync(ct);
            logger.LogInformation("Submission {Id} saved to database, publishing CodeSubmittedEvent", submission.Id);

            await publisher.Publish(new CodeSubmittedEvent(
                submission.Id,
                correlationId,
                submission.CreatedAt), ct);

            logger.LogInformation("CodeSubmittedEvent published for submission {Id}", submission.Id);
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

        app.MapGet("/api/submissions", async (int page, int pageSize, RoasterDbContext db, ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("Submissions");
            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 20 : pageSize;
            logger.LogInformation("Listing submissions page {Page}, pageSize {PageSize}", page, pageSize);

            var results = await db.Submissions
                .OrderByDescending(s => s.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(s => ToDto(s))
                .ToListAsync(ct);

            logger.LogInformation("Returning {Count} submissions", results.Count);
            return Results.Ok(results);
        }).WithName("ListSubmissions");
    }

    private static SubmissionDto ToDto(Submission s) =>
        new(s.Id, s.Payload.Language, s.Status.ToString(), s.CreatedAt, s.UpdatedAt);
}

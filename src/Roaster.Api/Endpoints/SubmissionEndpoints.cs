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
            IPublishEndpoint publisher, HttpContext ctx, CancellationToken ct) =>
        {
            var correlationId = (Guid)ctx.Items["CorrelationId"]!;
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

            await publisher.Publish(new CodeSubmittedEvent(
                submission.Id,
                correlationId,
                submission.CreatedAt), ct);

            return Results.AcceptedAtRoute("GetSubmission", new { id = submission.Id }, ToDto(submission));
        }).WithName("CreateSubmission");

        app.MapGet("/api/submissions/{id:long}", async (long id, RoasterDbContext db, CancellationToken ct) =>
        {
            var submission = await db.Submissions.FindAsync([id], ct);
            return submission is null ? Results.NotFound() : Results.Ok(ToDto(submission));
        }).WithName("GetSubmission");

        app.MapGet("/api/submissions", async (int page, int pageSize, RoasterDbContext db, CancellationToken ct) =>
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 20 : pageSize;

            return Results.Ok(await db.Submissions
                .OrderByDescending(s => s.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(s => ToDto(s))
                .ToListAsync(ct));
        }).WithName("ListSubmissions");
    }

    private static SubmissionDto ToDto(Submission s) =>
        new(s.Id, s.Payload.Language, s.Status.ToString(), s.CreatedAt, s.UpdatedAt);
}

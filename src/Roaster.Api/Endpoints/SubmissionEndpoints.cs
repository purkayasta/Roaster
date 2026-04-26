using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Roaster.Api.Models;
using Roaster.Contracts.Dtos;
using Roaster.Data.Context;
using Roaster.Data.Models;

namespace Roaster.Api.Endpoints;

public static class SubmissionEndpoints
{
    public static void MapSubmissionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/submissions", async (SubmissionRequest req, RoasterDbContext db, CancellationToken ct) =>
        {
            var sanitized = Regex.Replace(req.Code, @"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", "");

            var submission = new Submission
            {
                Payload = new CodePayload { Content = sanitized, Language = req.Language },
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.Submissions.Add(submission);
            await db.SaveChangesAsync(ct);

            var dto = ToDto(submission);
            return Results.AcceptedAtRoute("GetSubmission", new { id = submission.Id }, dto);
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

            var items = await db.Submissions
                .OrderByDescending(s => s.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(s => ToDto(s))
                .ToListAsync(ct);

            return Results.Ok(items);
        }).WithName("ListSubmissions");
    }

    private static SubmissionDto ToDto(Submission s) =>
        new(s.Id, s.Payload.Language, s.Status.ToString(), s.CreatedAt, s.UpdatedAt);
}

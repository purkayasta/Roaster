/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Roaster.Contracts.Dtos;
using Roaster.Data.Context;
using Roaster.Data.Models;

namespace Roaster.Api.Tests;

public sealed class ReviewEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private HttpClient Client => factory.CreateClient();

    private async Task<Submission> SeedSubmission(RoasterDbContext db, SubmissionStatus status = SubmissionStatus.Submitted)
    {
        var session = new Session { Name = "test", CreatedAt = DateTime.UtcNow };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();

        var sub = new Submission
        {
            SessionId = session.Id,
            Payload   = new CodePayload { Content = "x", Language = "csharp" },
            Status    = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Submissions.Add(sub);
        await db.SaveChangesAsync();
        return sub;
    }

    [Fact]
    public async Task GetReview_UnknownSubmission_Returns404()
    {
        var resp = await Client.GetAsync("/api/submissions/99999/review");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetReview_PendingSubmission_Returns204()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RoasterDbContext>();
        var sub = await SeedSubmission(db, SubmissionStatus.Submitted);

        var resp = await Client.GetAsync($"/api/submissions/{sub.Id}/review");
        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetReview_ProcessingSubmission_Returns204()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RoasterDbContext>();
        var sub = await SeedSubmission(db, SubmissionStatus.Processing);

        var resp = await Client.GetAsync($"/api/submissions/{sub.Id}/review");
        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetReview_CompletedSubmission_Returns200WithReviewDto()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RoasterDbContext>();
        var sub = await SeedSubmission(db, SubmissionStatus.Completed);

        var review = new Review
        {
            SubmissionId = sub.Id,
            Summary = "looks good",
            Comments = [new ReviewLineComment { LineNumber = 3, Comment = "use const", Severity = ReviewSeverity.Gentle }],
            CreatedAt = DateTime.UtcNow
        };
        db.Reviews.Add(review);
        await db.SaveChangesAsync();

        var resp = await Client.GetAsync($"/api/submissions/{sub.Id}/review");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await resp.Content.ReadFromJsonAsync<ReviewDto>();
        dto!.Summary.Should().Be("looks good");
        dto.Comments.Should().HaveCount(1);
        dto.Comments[0].LineNumber.Should().Be(3);
    }

    [Fact]
    public async Task GetReview_CommentsFromJsonb_MappedToDto()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RoasterDbContext>();
        var sub = await SeedSubmission(db, SubmissionStatus.Completed);

        var review = new Review
        {
            SubmissionId = sub.Id,
            Comments =
            [
                new ReviewLineComment { LineNumber = 1, Comment = "c1", Severity = ReviewSeverity.Gentle },
                new ReviewLineComment { LineNumber = 2, Comment = "c2", Severity = ReviewSeverity.Brutal }
            ],
            CreatedAt = DateTime.UtcNow
        };
        db.Reviews.Add(review);
        await db.SaveChangesAsync();

        var dto = await (await Client.GetAsync($"/api/submissions/{sub.Id}/review"))
            .Content.ReadFromJsonAsync<ReviewDto>();

        dto!.Comments.Should().HaveCount(2);
        dto.Comments.Should().Contain(c => c.Severity == "Gentle");
        dto.Comments.Should().Contain(c => c.Severity == "Brutal");
    }

    [Fact]
    public async Task GetReview_ThreadIdPopulated_WhenThreadExistsForLine()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RoasterDbContext>();
        var sub = await SeedSubmission(db, SubmissionStatus.Completed);

        var review = new Review
        {
            SubmissionId = sub.Id,
            Comments = [new ReviewLineComment { LineNumber = 5, Comment = "x", Severity = ReviewSeverity.Medium }],
            CreatedAt = DateTime.UtcNow
        };
        db.Reviews.Add(review);
        await db.SaveChangesAsync();

        var thread = new CommentThread { ReviewId = review.Id, LineNumber = 5, CreatedAt = DateTime.UtcNow };
        db.CommentThreads.Add(thread);
        await db.SaveChangesAsync();

        var dto = await (await Client.GetAsync($"/api/submissions/{sub.Id}/review"))
            .Content.ReadFromJsonAsync<ReviewDto>();

        dto!.Comments[0].ThreadId.Should().Be(thread.Id);
    }
}

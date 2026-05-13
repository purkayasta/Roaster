using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Roaster.Contracts.Dtos;
using Roaster.Data.Context;
using Roaster.Data.Models;

namespace Roaster.Api.Tests;

public class SubmissionEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private HttpClient Client => factory.CreateClient();

    // ── validation ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Post_EmptyCode_Returns400()
    {
        var resp = await Client.PostAsJsonAsync("/api/submissions", new { code = "", language = "csharp" });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_CodeOver50kChars_Returns400()
    {
        var resp = await Client.PostAsJsonAsync("/api/submissions",
            new { code = new string('x', 50_001), language = "csharp" });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_UnsupportedLanguage_Returns400()
    {
        var resp = await Client.PostAsJsonAsync("/api/submissions",
            new { code = "print('hi')", language = "brainfuck" });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_ValidRequest_Returns202()
    {
        var sessionResp = await Client.PostAsJsonAsync("/api/sessions", new { name = "test-session" });
        var session = await sessionResp.Content.ReadFromJsonAsync<SessionDto>();

        var resp = await Client.PostAsJsonAsync("/api/submissions",
            new { sessionId = session!.Id, code = "var x = 1;", language = "csharp" });
        resp.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    // ── sanitization ────────────────────────────────────────────────────────

    [Fact]
    public async Task Post_ControlCharactersStripped_BeforeSave()
    {
        var sessionResp = await Client.PostAsJsonAsync("/api/sessions", new { name = "sanitize-session" });
        var session = await sessionResp.Content.ReadFromJsonAsync<SessionDto>();

        var codeWithControl = "var x\x01 = 1\x1F;";  // contains control chars
        await Client.PostAsJsonAsync("/api/submissions",
            new { sessionId = session!.Id, code = codeWithControl, language = "csharp" });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RoasterDbContext>();
        var sub = db.Submissions.FirstOrDefault();

        sub.Should().NotBeNull();
        sub!.Payload.Content.Should().NotContain("\x01");
        sub.Payload.Content.Should().NotContain("\x1F");
    }

    // ── resubmit guard ──────────────────────────────────────────────────────

    [Fact]
    public async Task Post_WhileProcessing_Returns409()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RoasterDbContext>();
        var session = new Session { Name = "processing-session", CreatedAt = DateTime.UtcNow };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();

        var sub = new Submission
        {
            SessionId = session.Id,
            Payload   = new CodePayload { Content = "var x = 1;", Language = "csharp" },
            Status    = SubmissionStatus.Processing,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Submissions.Add(sub);
        await db.SaveChangesAsync();

        var resp = await Client.PostAsJsonAsync("/api/submissions",
            new { sessionId = session.Id, code = "var y = 2;", language = "csharp" });

        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ── GET /api/submissions/{id} ────────────────────────────────────────────

    [Fact]
    public async Task Get_UnknownId_Returns404()
    {
        var resp = await Client.GetAsync("/api/submissions/99999");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_KnownId_Returns200WithDto()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RoasterDbContext>();
        var session = new Session { Name = "get-test", CreatedAt = DateTime.UtcNow };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();

        var sub = new Submission
        {
            SessionId = session.Id,
            Payload   = new CodePayload { Content = "x", Language = "go" },
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Submissions.Add(sub);
        await db.SaveChangesAsync();

        var resp = await Client.GetAsync($"/api/submissions/{sub.Id}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await resp.Content.ReadFromJsonAsync<SubmissionDto>();
        dto!.Language.Should().Be("go");
    }
}

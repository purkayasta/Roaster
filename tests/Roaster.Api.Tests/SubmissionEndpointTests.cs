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

public sealed class SubmissionEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private HttpClient Client => factory.CreateClient();

    // ── validation ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Post_EmptyContent_Returns400()
    {
        var resp = await Client.PostAsJsonAsync("/api/submissions",
            new { files = new[] { new { content = "", language = "csharp" } } });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_ContentOver50kChars_Returns400()
    {
        var resp = await Client.PostAsJsonAsync("/api/submissions",
            new { files = new[] { new { content = new string('x', 50_001), language = "csharp" } } });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_UnsupportedLanguage_Returns400()
    {
        var resp = await Client.PostAsJsonAsync("/api/submissions",
            new { files = new[] { new { content = "print('hi')", language = "brainfuck" } } });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_ValidRequest_Returns202()
    {
        var sessionResp = await Client.PostAsJsonAsync("/api/sessions", new { name = "test-session" });
        var session = await sessionResp.Content.ReadFromJsonAsync<SessionDto>();

        var resp = await Client.PostAsJsonAsync("/api/submissions",
            new { sessionId = session!.Id, files = new[] { new { fileName = "main.cs", content = "var x = 1;", language = "csharp" } } });
        resp.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    // ── sanitization ────────────────────────────────────────────────────────

    [Fact]
    public async Task Post_ControlCharactersStripped_BeforeSave()
    {
        var sessionResp = await Client.PostAsJsonAsync("/api/sessions", new { name = "sanitize-session" });
        var session = await sessionResp.Content.ReadFromJsonAsync<SessionDto>();

        var codeWithControl = "var x\x01 = 1\x1F;";
        await Client.PostAsJsonAsync("/api/submissions",
            new { sessionId = session!.Id, files = new[] { new { fileName = "main.cs", content = codeWithControl, language = "csharp" } } });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RoasterDbContext>();
        var sub = db.Submissions.FirstOrDefault();

        sub.Should().NotBeNull();
        sub!.Payloads[0].Content.Should().NotContain("\x01");
        sub.Payloads[0].Content.Should().NotContain("\x1F");
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
            Payloads  = [new Payload { FileName = "main.cs", Content = "var x = 1;", Language = "csharp" }],
            Status    = SubmissionStatus.Processing,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Submissions.Add(sub);
        await db.SaveChangesAsync();

        var resp = await Client.PostAsJsonAsync("/api/submissions",
            new { sessionId = session.Id, files = new[] { new { fileName = "main.cs", content = "var y = 2;", language = "csharp" } } });

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
            Payloads  = [new Payload { FileName = "main.go", Content = "x", Language = "go" }],
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Submissions.Add(sub);
        await db.SaveChangesAsync();

        var resp = await Client.GetAsync($"/api/submissions/{sub.Id}");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await resp.Content.ReadFromJsonAsync<SubmissionDto>();
        dto!.FileNames.Should().ContainSingle(f => f == "main.go");
    }
}

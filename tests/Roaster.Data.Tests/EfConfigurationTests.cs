/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Roaster.Data.Context;
using Roaster.Data.Models;

namespace Roaster.Data.Tests;

public sealed class EfConfigurationTests
{
    private static RoasterDbContext BuildDb() =>
        new(new DbContextOptionsBuilder<RoasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public void Submission_Status_IsStoredAsInt()
    {
        using var db = BuildDb();
        var entityType = db.Model.FindEntityType(typeof(Submission))!;
        var prop = entityType.FindProperty(nameof(Submission.Status))!;

        // HasConversion<int>() configures a provider type of int; verify it's not stored as string
        prop.ClrType.Should().Be(typeof(SubmissionStatus));
        // The configured provider CLR type should be int, not the enum itself
        prop.GetProviderClrType().Should().Be(typeof(int));
    }

    [Fact]
    public void Review_Comments_IsMappedToJson()
    {
        using var db = BuildDb();
        var entityType = db.Model.FindEntityType(typeof(Review))!;
        var nav = entityType.FindNavigation(nameof(Review.Comments));

        // ToJson() makes Comments an owned navigation stored as JSON
        nav.Should().NotBeNull();
        nav!.ForeignKey.DeclaringEntityType.IsOwned().Should().BeTrue();
    }

    [Fact]
    public void Event_Metadata_IsNullable()
    {
        using var db = BuildDb();
        var entityType = db.Model.FindEntityType(typeof(Event))!;
        var prop = entityType.FindProperty(nameof(Event.Metadata))!;

        prop.IsNullable.Should().BeTrue();
    }

    [Fact]
    public async Task SaveChangesAsync_UpdatesSubmissionUpdatedAt()
    {
        using var db = BuildDb();

        var session = new Session { Name = "test", CreatedAt = DateTime.UtcNow };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();

        var sub = new Submission
        {
            SessionId = session.Id,
            Payloads  = [new Payload { FileName = "main.cs", Content = "x", Language = "csharp" }],
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };
        db.Submissions.Add(sub);
        await db.SaveChangesAsync();

        var originalUpdated = sub.UpdatedAt;

        sub.Status = SubmissionStatus.Processing;
        await db.SaveChangesAsync();

        sub.UpdatedAt.Should().BeAfter(originalUpdated);
    }
}

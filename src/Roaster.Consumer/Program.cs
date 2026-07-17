/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Roaster.Consumer.Processors;
using Roaster.Consumer.Services;
using Roaster.Data.Context;
using Roaster.Reviewer;
using Roaster.Reviewer.Options;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(builder.Configuration).WriteTo.Console());

builder.Services.AddDbContext<RoasterDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.Configure<OllamaOptions>(builder.Configuration.GetSection("Ollama"));
builder.Services.AddAiReviewer();

builder.Services.AddScoped<CodeReviewService>();
builder.Services.AddScoped<ThreadReplyService>();

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<ReviewProcessor>();
    x.AddConsumer<ReplyProcessor>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Uri"]);

        cfg.ReceiveEndpoint("code-submitted", e =>
        {
            e.PrefetchCount = 1;
            e.ConcurrentMessageLimit = 1;
            e.ConfigureConsumer<ReviewProcessor>(ctx);
            e.UseMessageRetry(r => r.Exponential(3,
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(120),
                TimeSpan.FromSeconds(10)));
        });

        cfg.ReceiveEndpoint("user-thread-reply", e =>
        {
            e.ConfigureConsumer<ReplyProcessor>(ctx);
            e.UseMessageRetry(r => r.Intervals(
                TimeSpan.FromSeconds(3),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30)));
        });
    });
});

await builder.Build().RunAsync();

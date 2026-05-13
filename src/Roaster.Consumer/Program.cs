using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Roaster.Consumer.Consumers;
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

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<CodeSubmittedConsumer>();
    x.AddConsumer<UserThreadReplyConsumer>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Uri"]);

        cfg.ReceiveEndpoint("code-submitted", e =>
        {
            e.ConcurrentMessageLimit = 1;
            e.PrefetchCount = 1;
            e.ConfigureConsumer<CodeSubmittedConsumer>(ctx);
            e.UseMessageRetry(r => r.Exponential(3,
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(120),
                TimeSpan.FromSeconds(10)));
        });

        cfg.ReceiveEndpoint("user-thread-reply", e =>
        {
            e.ConfigureConsumer<UserThreadReplyConsumer>(ctx);
            e.UseMessageRetry(r => r.Intervals(
                TimeSpan.FromSeconds(3),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30)));
        });
    });
});

await builder.Build().RunAsync();

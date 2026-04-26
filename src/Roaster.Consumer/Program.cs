using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Roaster.Consumer.Consumers;
using Roaster.Data.Context;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(builder.Configuration).WriteTo.Console());

builder.Services.AddDbContext<RoasterDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<CodeSubmittedConsumer>();
    x.AddConsumer<UserThreadReplyConsumer>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Uri"]);

        cfg.ReceiveEndpoint("code-submitted", e =>
        {
            e.ConfigureConsumer<CodeSubmittedConsumer>(ctx);
            e.UseMessageRetry(r => r.Exponential(5,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(60),
                TimeSpan.FromSeconds(5)));
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

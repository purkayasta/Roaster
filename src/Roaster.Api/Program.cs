using MassTransit;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using Roaster.Api.Endpoints;
using Roaster.Api.Middleware;
using Roaster.Data.Context;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration).WriteTo.Console());

builder.Services.AddDbContext<RoasterDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddCors(opts =>
    opts.AddDefaultPolicy(p =>
        p.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>()!)
         .AllowAnyHeader()
         .AllowAnyMethod()));

builder.Services.AddMassTransit(x =>
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Uri"]);
        cfg.ConfigureEndpoints(ctx);
    }));

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Postgres")!)
    .AddRabbitMQ(sp =>
    {
        var amqpUri = builder.Configuration["RabbitMQ:Uri"]!.Replace("rabbitmq://", "amqp://");
        var factory = new ConnectionFactory { Uri = new Uri(amqpUri) };
        return factory.CreateConnectionAsync();
    });

builder.Services.AddOpenApi();
builder.Services.AddValidation();

var app = builder.Build();
app.UseCors();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();

app.MapScalarApiReference();

app.MapOpenApi();
app.MapSessionEndpoints();
app.MapSubmissionEndpoints();
app.MapReviewEndpoints();
app.MapThreadEndpoints();
app.MapEventEndpoints();
app.MapHealthChecks("/health");

await app.RunAsync();

public partial class Program { }

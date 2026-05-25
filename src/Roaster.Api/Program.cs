/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
using System.IO.Compression;
using MassTransit;
using Microsoft.AspNetCore.ResponseCompression;
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

builder.Services.AddResponseCompression(opts =>
{
    opts.EnableForHttps = true;
    opts.Providers.Add<BrotliCompressionProvider>();
    opts.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(opts => opts.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(opts => opts.Level = CompressionLevel.Fastest);

builder.Services.AddOpenApi();
builder.Services.AddScoped<Roaster.Api.Services.SessionService>();
builder.Services.AddScoped<Roaster.Api.Services.SubmissionService>();
builder.Services.AddScoped<Roaster.Api.Services.ReviewService>();
builder.Services.AddScoped<Roaster.Api.Services.ThreadService>();
builder.Services.AddScoped<Roaster.Api.Services.EventService>();
builder.Services.AddValidation();

var app = builder.Build();
app.UseResponseCompression();
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

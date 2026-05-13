using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Roaster.Data.Context;

namespace Roaster.Api.Tests;

public class ApiFactory : WebApplicationFactory<Program>
{
    // Shared root ensures test-scope writes are visible inside HTTP request scopes
    private static readonly InMemoryDatabaseRoot DbRoot = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the Npgsql DbContext configuration so InMemory is the only provider
            var toRemove = services
                .Where(d => d.ServiceType == typeof(IDbContextOptionsConfiguration<RoasterDbContext>))
                .ToList();
            foreach (var d in toRemove) services.Remove(d);

            // Suppress TransactionIgnoredWarning — InMemory ignores transactions by design
            services.AddDbContext<RoasterDbContext>(opts =>
                opts.UseInMemoryDatabase("ApiTests", DbRoot)
                    .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));

            // Remove the RabbitMQ bus (tries to connect on startup) and replace publisher with no-op mock
            var masstransitServices = services
                .Where(d => d.ServiceType == typeof(IBusControl)
                         || d.ServiceType == typeof(IBus)
                         || d.ServiceType == typeof(IPublishEndpoint))
                .ToList();
            foreach (var d in masstransitServices) services.Remove(d);

            // Remove MassTransit hosted services (prevents RabbitMQ connection attempts)
            var hostedServices = services
                .Where(d => d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)
                         && d.ImplementationType?.Namespace?.StartsWith("MassTransit") == true)
                .ToList();
            foreach (var d in hostedServices) services.Remove(d);

            var mockPublisher = new Mock<IPublishEndpoint>();
            services.AddSingleton(mockPublisher.Object);
            services.AddSingleton<IBus>(_ => new Mock<IBus>().Object);
        });

        builder.UseEnvironment("Development");
    }
}

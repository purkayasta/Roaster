using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.Connectors.Ollama;
using Roaster.Reviewer.Abstractions;
using Roaster.Reviewer.Options;
using Roaster.Reviewer.Services;

namespace Roaster.Reviewer;

public static class ReviewerExtensions
{
    public static IServiceCollection AddAiReviewer(this IServiceCollection services)
    {
        services.AddSingleton<IAiReviewService>(sp =>
        {
            var opts         = sp.GetRequiredService<IOptions<OllamaOptions>>().Value;
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var chat         = new OllamaChatCompletionService(opts.ModelId, new Uri(opts.Endpoint), loggerFactory);
            return new SemanticKernelAiReviewService(chat, loggerFactory.CreateLogger<SemanticKernelAiReviewService>());
        });
        return services;
    }
}

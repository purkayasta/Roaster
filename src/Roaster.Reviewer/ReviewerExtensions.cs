/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.Connectors.Ollama;
using Roaster.Reviewer.Abstractions;
using Roaster.Reviewer.Options;
using Roaster.Reviewer.Prompts;
using Roaster.Reviewer.Services;

namespace Roaster.Reviewer;

public static class ReviewerExtensions
{
    public static IServiceCollection AddAiReviewer(this IServiceCollection services)
    {
        services.AddSingleton<IAiReviewService>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<OllamaOptions>>().Value;
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var chat = new OllamaChatCompletionService(opts.ModelId, new Uri(opts.Endpoint), loggerFactory);
            var agentLogger = loggerFactory.CreateLogger<ChatReviewAgent>();

            // Provider-neutral OllamaOptions → connector-specific settings. On the
            // SK→MAF swap, this becomes a ChatOptions built from the same options.
            var settings = new OllamaPromptExecutionSettings
            {
                Temperature = (float)opts.Temperature,
                NumPredict  = opts.MaxOutputTokens,
            };

            IReadOnlyList<IReviewAgent> agents =
            [
                new ChatReviewAgent("Security", PromptLoader.Security, chat, settings, agentLogger),
                new ChatReviewAgent("Bug", PromptLoader.Bug, chat, settings, agentLogger),
                new ChatReviewAgent("Syntax", PromptLoader.Syntax, chat, settings, agentLogger),
            ];

            return new AiReviewService(agents, chat, loggerFactory.CreateLogger<AiReviewService>());
        });
        return services;
    }
}

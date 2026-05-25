/// <summary>
/// Author: Pritom Purkayasta
//  Copyright (c) Pritom Purkayasta All rights reserved.
//  FREE TO USE TO CONNECT THE WORLD
/// </summary>
using MassTransit;
using Roaster.Consumer.Services;
using Roaster.Contracts.Events;
using SerilogContext = Serilog.Context.LogContext;

namespace Roaster.Consumer.Processors;

public sealed class ReviewProcessor(CodeReviewService service) : IConsumer<SubmissionCreatedEvent>
{
    public async Task Consume(ConsumeContext<SubmissionCreatedEvent> context)
    {
        var msg = context.Message;

        using (SerilogContext.PushProperty("CorrelationId", msg.CorrelationId))
        using (SerilogContext.PushProperty("SubmissionId", msg.SubmissionId))
        {
            await service.ProcessAsync(msg, context.CancellationToken);
        }
    }
}

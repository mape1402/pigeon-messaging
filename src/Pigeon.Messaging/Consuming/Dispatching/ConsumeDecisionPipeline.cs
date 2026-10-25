namespace Pigeon.Messaging.Consuming.Dispatching
{
    using System.Diagnostics.Metrics;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Pigeon.Messaging.Consuming.Configuration;

    internal static class ConsumeDecisionPipeline
    {
        private static readonly Meter Meter = new("Pigeon.Messaging.ConsumeDecisions");
        private static readonly Counter<long> TerminalDecisions = Meter.CreateCounter<long>("pigeon.consume.decisions.terminal");
        private static readonly Counter<long> ObserverExecutions = Meter.CreateCounter<long>("pigeon.consume.observers.executions");
        private static readonly Counter<long> ObserverFailures = Meter.CreateCounter<long>("pigeon.consume.observers.failures");
        private static readonly Counter<long> ObserverRetries = Meter.CreateCounter<long>("pigeon.consume.observers.retries");

        internal static async ValueTask<PigeonConsumeDecisionResult> ResolveAsync(
            ConsumeContext context, PigeonRouteKey route, PigeonRouteInterceptorRegistry registry, CancellationToken cancellationToken)
        {
            foreach (var interceptor in context.Services.GetServices<IConsumeDecisionInterceptor>())
            {
                var result = await interceptor.InterceptAsync(context, cancellationToken) ?? PigeonConsumeDecisionResult.Continue;
                if (result.Decision != PigeonConsumeDecision.Continue)
                    return RecordDecision(context, result, interceptor.GetType());
            }

            if (registry != null)
            {
                foreach (var type in registry.GetConsumeDecisionInterceptors(route))
                {
                    var interceptor = (IConsumeDecisionInterceptor)context.Services.GetRequiredService(type);
                    var result = await interceptor.InterceptAsync(context, cancellationToken) ?? PigeonConsumeDecisionResult.Continue;
                    if (result.Decision != PigeonConsumeDecision.Continue)
                        return RecordDecision(context, result, interceptor.GetType());
                }
            }

            return PigeonConsumeDecisionResult.Continue;
        }

        internal static async ValueTask ObserveAsync(
            ConsumeContext context, PigeonConsumeDecisionResult result, PigeonRouteKey route,
            PigeonRouteInterceptorRegistry registry, CancellationToken cancellationToken)
        {
            if (result.Metadata != null)
                foreach (var item in result.Metadata)
                    context.ReplyMetadata[item.Key] = item.Value;

            var observation = new ConsumeDecisionObservation(context, result);
            foreach (var observer in context.Services.GetServices<IConsumeDecisionObserver>())
                await ExecuteObserverAsync(observer, observation, cancellationToken);

            if (registry != null)
                foreach (var type in registry.GetConsumeDecisionObservers(route))
                    await ExecuteObserverAsync((IConsumeDecisionObserver)context.Services.GetRequiredService(type), observation, cancellationToken);
        }

        internal static void RecordObserverRetry(ConsumeContext context)
        {
            ObserverRetries.Add(1);
            Logger(context).LogWarning("Retrying after observer failure for topic {Topic}, version {Version}, subscription {Subscription}, message {MessageId}, correlation {CorrelationId}",
                context.Topic, context.MessageVersion, context.Subscription, context.MessageId, context.CorrelationId);
        }

        internal static void RecordSettlement(ConsumeContext context, PigeonConsumeDecisionResult result)
            => Logger(context).LogInformation("Applied settlement {Decision} for topic {Topic}, version {Version}, subscription {Subscription}, message {MessageId}, correlation {CorrelationId}",
                result.Decision, context.Topic, context.MessageVersion, context.Subscription, context.MessageId, context.CorrelationId);

        private static PigeonConsumeDecisionResult RecordDecision(ConsumeContext context, PigeonConsumeDecisionResult result, Type interceptorType)
        {
            TerminalDecisions.Add(1);
            Logger(context).LogInformation("Terminal decision {Decision} by interceptor {Interceptor} with outcome {Outcome} for topic {Topic}, version {Version}, subscription {Subscription}, message {MessageId}, correlation {CorrelationId}",
                result.Decision, interceptorType.FullName, result.Outcome?.Kind ?? PigeonConsumeOutcomeKind.Unspecified,
                context.Topic, context.MessageVersion, context.Subscription, context.MessageId, context.CorrelationId);
            return result;
        }

        private static async ValueTask ExecuteObserverAsync(IConsumeDecisionObserver observer, ConsumeDecisionObservation observation, CancellationToken cancellationToken)
        {
            var context = observation.Context;
            ObserverExecutions.Add(1);
            Logger(context).LogInformation("Executing observer {Observer} for topic {Topic}, version {Version}, subscription {Subscription}, message {MessageId}, correlation {CorrelationId}",
                observer.GetType().FullName, context.Topic, context.MessageVersion, context.Subscription, context.MessageId, context.CorrelationId);
            try
            {
                await observer.ObserveAsync(observation, cancellationToken);
            }
            catch (Exception exception)
            {
                ObserverFailures.Add(1);
                Logger(context).LogError(exception, "Observer {Observer} failed for topic {Topic}, version {Version}, subscription {Subscription}, message {MessageId}, correlation {CorrelationId}",
                    observer.GetType().FullName, context.Topic, context.MessageVersion, context.Subscription, context.MessageId, context.CorrelationId);
                throw;
            }
        }

        private static ILogger<ConsumeDecisionObservation> Logger(ConsumeContext context)
            => context.Services.GetService<ILogger<ConsumeDecisionObservation>>() ?? NullLogger<ConsumeDecisionObservation>.Instance;
    }
}

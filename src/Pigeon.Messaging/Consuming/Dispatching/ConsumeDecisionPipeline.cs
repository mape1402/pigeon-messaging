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
        private static readonly Counter<long> ObserverIgnoredFailures = Meter.CreateCounter<long>("pigeon.consume.observers.failures.continued");
        private static readonly Counter<long> ObserverCriticalFailures = Meter.CreateCounter<long>("pigeon.consume.observers.failures.critical");

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
            {
                var configured = observer as ConfiguredConsumeDecisionObserver;
                await ExecuteObserverAsync(() => observer, configured?.ObserverType ?? observer.GetType(),
                    configured?.Behavior ?? ObserverFailureBehavior.Fail, observation, cancellationToken);
            }

            if (registry != null)
                foreach (var registration in registry.GetConsumeDecisionObserverRegistrations(route))
                    await ExecuteObserverAsync(() => (IConsumeDecisionObserver)context.Services.GetRequiredService(registration.Type),
                        registration.Type, registration.Behavior, observation, cancellationToken);
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

        private static async ValueTask ExecuteObserverAsync(Func<IConsumeDecisionObserver> resolve, Type observerType,
            ObserverFailureBehavior behavior, ConsumeDecisionObservation observation, CancellationToken cancellationToken)
        {
            var context = observation.Context;
            ObserverExecutions.Add(1);
            Logger(context).LogInformation("Executing observer {Observer} with failure behavior {FailureBehavior} for topic {Topic}, version {Version}, subscription {Subscription}, message {MessageId}, correlation {CorrelationId}",
                observerType.FullName, behavior, context.Topic, context.MessageVersion, context.Subscription, context.MessageId, context.CorrelationId);
            try
            {
                await resolve().ObserveAsync(observation, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                ObserverFailures.Add(1);
                if (behavior == ObserverFailureBehavior.Continue)
                    ObserverIgnoredFailures.Add(1);
                else
                    ObserverCriticalFailures.Add(1);
                Logger(context).LogError(exception, "Observer {Observer} failed with behavior {FailureBehavior} for topic {Topic}, version {Version}, subscription {Subscription}, message {MessageId}, correlation {CorrelationId}",
                    observerType.FullName, behavior, context.Topic, context.MessageVersion, context.Subscription, context.MessageId, context.CorrelationId);
                if (behavior == ObserverFailureBehavior.Fail)
                    throw;
            }
        }

        private static ILogger<ConsumeDecisionObservation> Logger(ConsumeContext context)
            => context.Services.GetService<ILogger<ConsumeDecisionObservation>>() ?? NullLogger<ConsumeDecisionObservation>.Instance;
    }
}

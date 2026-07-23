using Pigeon.Messaging.Contracts;
using Pigeon.Messaging.Producing;
using Pigeon.Messaging.Producing.Management;

internal sealed class CapturingProducingAdapter : IMessageBrokerProducingAdapter
{
    private readonly PublishedMessages _publishedMessages;

    public CapturingProducingAdapter(PublishedMessages publishedMessages)
    {
        _publishedMessages = publishedMessages;
    }

    public ValueTask PublishMessageAsync<T>(WrappedPayload<T> payload, string topic, CancellationToken cancellationToken = default)
        where T : class
        => PublishMessageAsync(payload, PublishingRoute.ForTopic(topic), cancellationToken);

    public ValueTask PublishMessageAsync<T>(WrappedPayload<T> payload, PublishingRoute route, CancellationToken cancellationToken = default)
        where T : class
    {
        _publishedMessages.Add(new PublishedEnvelope("wrapped", typeof(T).Name, Describe(payload.Message), route));
        return ValueTask.CompletedTask;
    }

    public ValueTask PublishRawMessageAsync<T>(T message, string topic, CancellationToken cancellationToken = default)
        where T : class
        => PublishRawMessageAsync(message, PublishingRoute.ForTopic(topic), cancellationToken);

    public ValueTask PublishRawMessageAsync<T>(T message, PublishingRoute route, CancellationToken cancellationToken = default)
        where T : class
    {
        _publishedMessages.Add(new PublishedEnvelope("raw", typeof(T).Name, Describe(message), route));
        return ValueTask.CompletedTask;
    }

    private static string Describe<T>(T message)
        => message is SampleMessage sample ? sample.Text : typeof(T).Name;
}

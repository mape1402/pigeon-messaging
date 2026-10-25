namespace Pigeon.Messaging.Kafka.Tests.Consuming;

using Confluent.Kafka;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Pigeon.Messaging.Consuming.Configuration;
using Pigeon.Messaging.Kafka.Consuming;
using System.Reflection;

public class KafkaListenerTests
{
    [Fact]
    public async Task Listener_SkipsEmptyPolls()
    {
        using var cancellation = new CancellationTokenSource();
        var consumer = Substitute.For<IConsumer<Ignore, string>>();
        consumer.Consume(Arg.Any<TimeSpan>()).Returns(_ => null, _ => Result());
        var adapter = CreateAdapter();
        var received = 0;
        adapter.MessageConsumedAsync += (_, args, _) => { received++; cancellation.Cancel(); return ValueTask.CompletedTask; };
        await ListenAsync(adapter, consumer, cancellation.Token);
        Assert.Equal(1, received);
    }

    [Fact]
    public async Task Listener_ContinuesAfterConsumeFailure()
    {
        using var cancellation = new CancellationTokenSource();
        var consumer = Substitute.For<IConsumer<Ignore, string>>();
        consumer.Consume(Arg.Any<TimeSpan>()).Returns(_ => throw new InvalidOperationException(), _ => Result());
        var adapter = CreateAdapter();
        adapter.MessageConsumedAsync += (_, _, _) => { cancellation.Cancel(); return ValueTask.CompletedTask; };
        await ListenAsync(adapter, consumer, cancellation.Token);
        consumer.Received(2).Consume(Arg.Any<TimeSpan>());
    }

    [Fact]
    public async Task Listener_StopsOnCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var consumer = Substitute.For<IConsumer<Ignore, string>>();
        consumer.Consume(Arg.Any<TimeSpan>()).Returns(_ => { cancellation.Cancel(); throw new OperationCanceledException(); });
        await ListenAsync(CreateAdapter(), consumer, cancellation.Token);
        consumer.Received(1).Consume(Arg.Any<TimeSpan>());
    }

    private static KafkaConsumingAdapter CreateAdapter()
        => new(Substitute.For<IConsumingConfigurator>(), Substitute.For<IConfigurationProvider>(),
            Options.Create(new GlobalSettings()), NullLogger<KafkaConsumingAdapter>.Instance);

    private static Task ListenAsync(KafkaConsumingAdapter adapter, IConsumer<Ignore, string> consumer, CancellationToken cancellationToken)
        => (Task)typeof(KafkaConsumingAdapter).GetMethod("ListenAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(adapter, new object[] { consumer, cancellationToken })!;

    private static ConsumeResult<Ignore, string> Result() => new()
    {
        Topic = "orders", Partition = new Partition(0), Offset = new Offset(1), Message = new Message<Ignore, string> { Value = "{}" }
    };
}

namespace Pigeon.Messaging.Kafka.Tests.Producing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Pigeon.Messaging.Kafka.Producing;

public class KafkaProducingFailureTests
{
    [Fact]
    public void Constructor_RequiresProvider()
        => Assert.Throws<ArgumentNullException>(() => new KafkaProducingAdapter(null, NullLogger<KafkaProducingAdapter>.Instance));

    [Fact]
    public void Constructor_RequiresLogger()
        => Assert.Throws<ArgumentNullException>(() => new KafkaProducingAdapter(Substitute.For<IServiceProvider>(), null));

    [Fact]
    public async Task PublishRaw_PropagatesMissingProducer()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var adapter = new KafkaProducingAdapter(services, NullLogger<KafkaProducingAdapter>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.PublishRawMessageAsync("hello", "orders").AsTask());
    }
}

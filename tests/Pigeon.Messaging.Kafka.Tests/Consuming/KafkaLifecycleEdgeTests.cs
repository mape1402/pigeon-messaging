namespace Pigeon.Messaging.Kafka.Tests.Consuming;

using Confluent.Kafka;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Pigeon.Messaging.Consuming.Configuration;
using Pigeon.Messaging.Kafka.Consuming;
using Pigeon.Messaging.Topology;
using System.Collections.Concurrent;
using System.Reflection;

public class KafkaLifecycleEdgeTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Constructor_RejectsMissingDependency(int missing)
    {
        var options = Substitute.For<IOptions<GlobalSettings>>();
        options.Value.Returns(missing == 3 ? null : new GlobalSettings());
        Assert.Throws<ArgumentNullException>(() => new KafkaConsumingAdapter(
            missing == 0 ? null : Substitute.For<IConsumingConfigurator>(),
            missing == 1 ? null : Substitute.For<IConfigurationProvider>(),
            missing == 2 ? null : Substitute.For<ITopologyProvisioningService>(),
            options,
            missing == 4 ? null : NullLogger<KafkaConsumingAdapter>.Instance));
    }

    [Fact]
    public async Task Stop_ContinuesWhenListenerFaults()
    {
        var adapter = CreateAdapter();
        var listeners = Field<ConcurrentDictionary<string, Task>>(adapter, "_listeners");
        listeners["orders::billing"] = Task.FromException(new InvalidOperationException());
        await adapter.StopConsumeAsync();
        Assert.Empty(listeners);
    }

    [Fact]
    public async Task Stop_ContinuesWhenConsumerCloseFails()
    {
        var adapter = CreateAdapter();
        var consumer = Substitute.For<IConsumer<Ignore, string>>();
        consumer.When(x => x.Close()).Do(_ => throw new InvalidOperationException());
        var consumers = Field<ConcurrentDictionary<string, IConsumer<Ignore, string>>>(adapter, "_consumers");
        consumers["orders::billing"] = consumer;
        await adapter.StopConsumeAsync();
        consumer.Received(1).Close();
        Assert.Empty(consumers);
    }

    [Fact]
    public async Task Stop_ClosesAndDisposesConsumer()
    {
        var adapter = CreateAdapter();
        var consumer = Substitute.For<IConsumer<Ignore, string>>();
        Field<ConcurrentDictionary<string, IConsumer<Ignore, string>>>(adapter, "_consumers")["orders::billing"] = consumer;
        await adapter.StopConsumeAsync();
        consumer.Received(1).Close();
        consumer.Received(1).Dispose();
    }

    [Fact]
    public async Task Start_DoesNotKeepDuplicateConsumers()
    {
        var configurator = Substitute.For<IConsumingConfigurator>();
        configurator.GetAllEndpoints().Returns(new[] { new ConsumerEndpoint("orders"), new ConsumerEndpoint("orders") });
        var configuration = Substitute.For<IConfigurationProvider>();
        configuration.GetConsumerConfig().Returns(new ConsumerConfig { BootstrapServers = "127.0.0.1:1", GroupId = "tests" });
        var adapter = new KafkaConsumingAdapter(configurator, configuration, Options.Create(new GlobalSettings()), NullLogger<KafkaConsumingAdapter>.Instance);
        await adapter.StartConsumeAsync();
        Assert.Single(Field<ConcurrentDictionary<string, IConsumer<Ignore, string>>>(adapter, "_consumers"));
        await adapter.StopConsumeAsync();
    }

    private static KafkaConsumingAdapter CreateAdapter()
        => new(Substitute.For<IConsumingConfigurator>(), Substitute.For<IConfigurationProvider>(),
            Options.Create(new GlobalSettings()), NullLogger<KafkaConsumingAdapter>.Instance);

    private static T Field<T>(KafkaConsumingAdapter adapter, string name)
        => (T)typeof(KafkaConsumingAdapter).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(adapter)!;
}

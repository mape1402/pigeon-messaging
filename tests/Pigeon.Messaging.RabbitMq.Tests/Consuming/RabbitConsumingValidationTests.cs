namespace Pigeon.Messaging.RabbitMq.Tests.Consuming;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Pigeon.Messaging.Consuming.Configuration;
using Pigeon.Messaging.Consuming.Management;
using Pigeon.Messaging.Rabbit;
using Pigeon.Messaging.Rabbit.Consuming;
using Pigeon.Messaging.Topology;
using RabbitMQ.Client;

public class RabbitConsumingValidationTests
{
    private readonly IConnectionProvider _connection = Substitute.For<IConnectionProvider>();
    private readonly IConsumingConfigurator _configurator = Substitute.For<IConsumingConfigurator>();
    private readonly ITopologyProvisioningService _topology = Substitute.For<ITopologyProvisioningService>();
    private readonly IOptions<GlobalSettings> _global = Options.Create(new GlobalSettings());
    private readonly IOptions<RabbitSettings> _settings = Options.Create(new RabbitSettings());
    private readonly ILogger<RabbitConsumingAdapter> _logger = NullLogger<RabbitConsumingAdapter>.Instance;

    [Fact]
    public void Constructor_RequiresConnection() => Assert.Throws<ArgumentNullException>(() => new RabbitConsumingAdapter(null, _configurator, _topology, _global, _settings, _logger));

    [Fact]
    public void Constructor_RequiresConfigurator() => Assert.Throws<ArgumentNullException>(() => new RabbitConsumingAdapter(_connection, null, _topology, _global, _settings, _logger));

    [Fact]
    public void Constructor_RequiresTopology() => Assert.Throws<ArgumentNullException>(() => new RabbitConsumingAdapter(_connection, _configurator, null, _global, _settings, _logger));

    [Fact]
    public void Constructor_RequiresGlobalSettings() => Assert.Throws<ArgumentNullException>(() => new RabbitConsumingAdapter(_connection, _configurator, _topology, null, _settings, _logger));

    [Fact]
    public void Constructor_RequiresRabbitSettings() => Assert.Throws<ArgumentNullException>(() => new RabbitConsumingAdapter(_connection, _configurator, _topology, _global, null, _logger));

    [Fact]
    public void Constructor_RequiresLogger() => Assert.Throws<ArgumentNullException>(() => new RabbitConsumingAdapter(_connection, _configurator, _topology, _global, _settings, null));

    [Fact]
    public async Task Start_ClampsDerivedPrefetch()
    {
        var channel = Substitute.For<IChannel>();
        _connection.CreateChannelAsync(Arg.Any<CancellationToken>()).Returns(channel);
        _configurator.GetAllEndpoints().Returns(new[] { new ConsumerEndpoint("orders") });
        var settings = Options.Create(new GlobalSettings { ConsumerExecution = new ConsumerExecutionSettings { MaxConcurrency = 100000 } });
        var adapter = new RabbitConsumingAdapter(_connection, _configurator, _topology, settings, _settings, _logger);
        await adapter.StartConsumeAsync();
        await channel.Received(1).BasicQosAsync(0, ushort.MaxValue, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Stop_HandlesChannelCloseFailure()
    {
        var channel = Substitute.For<IChannel>();
        channel.IsOpen.Returns(true);
        channel.CloseAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException(new InvalidOperationException()));
        _connection.CreateChannelAsync(Arg.Any<CancellationToken>()).Returns(channel);
        _configurator.GetAllEndpoints().Returns(new[] { new ConsumerEndpoint("orders") });
        var adapter = new RabbitConsumingAdapter(_connection, _configurator, _topology, _global, _settings, _logger);
        await adapter.StartConsumeAsync();
        await adapter.StopConsumeAsync();
        await channel.Received(1).CloseAsync(Arg.Any<CancellationToken>());
    }
}

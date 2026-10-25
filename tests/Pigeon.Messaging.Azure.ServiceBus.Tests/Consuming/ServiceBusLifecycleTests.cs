namespace Pigeon.Messaging.Azure.ServiceBus.Tests.Consuming;

using global::Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Pigeon.Messaging.Azure.ServiceBus.Consuming;
using Pigeon.Messaging.Consuming.Configuration;

public class ServiceBusLifecycleTests
{
    [Fact]
    public async Task Start_WithoutConfiguredEndpointsOrTopicsDoesNotCreateProcessor()
    {
        var configurator = Substitute.For<IConsumingConfigurator>();
        configurator.GetAllEndpoints().Returns((IEnumerable<ConsumerEndpoint>)null);
        configurator.GetAllTopics().Returns((IEnumerable<string>)null);
        var provider = Substitute.For<IServiceBusProvider>();
        var adapter = new ServiceBusConsumingAdapter(configurator, provider, Options.Create(new GlobalSettings()), NullLogger<ServiceBusConsumingAdapter>.Instance);

        await adapter.StartConsumeAsync();

        provider.DidNotReceive().CreateProcessor(Arg.Any<string>());
        provider.DidNotReceive().CreateProcessor(Arg.Any<string>(), Arg.Any<string>());
    }
    [Fact]
    public async Task Start_DuplicateEndpointDisposesExtraProcessor()
    {
        var configurator = Substitute.For<IConsumingConfigurator>();
        configurator.GetAllEndpoints().Returns(new[] { new ConsumerEndpoint("orders"), new ConsumerEndpoint("orders") });
        var first = Substitute.For<ServiceBusProcessor>();
        var duplicate = Substitute.For<ServiceBusProcessor>();
        var provider = Substitute.For<IServiceBusProvider>();
        provider.CreateProcessor("orders").Returns(first, duplicate);
        var adapter = new ServiceBusConsumingAdapter(configurator, provider, Options.Create(new GlobalSettings()), NullLogger<ServiceBusConsumingAdapter>.Instance);
        await adapter.StartConsumeAsync();
        await duplicate.Received(1).DisposeAsync();
    }

    [Fact]
    public async Task Stop_HandlesProcessorFailure()
    {
        var configurator = Substitute.For<IConsumingConfigurator>();
        configurator.GetAllEndpoints().Returns(new[] { new ConsumerEndpoint("orders") });
        var processor = Substitute.For<ServiceBusProcessor>();
        processor.StopProcessingAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException(new InvalidOperationException()));
        var provider = Substitute.For<IServiceBusProvider>();
        provider.CreateProcessor("orders").Returns(processor);
        var adapter = new ServiceBusConsumingAdapter(configurator, provider, Options.Create(new GlobalSettings()), NullLogger<ServiceBusConsumingAdapter>.Instance);
        await adapter.StartConsumeAsync();
        await adapter.StopConsumeAsync();
        await processor.Received(1).StopProcessingAsync(Arg.Any<CancellationToken>());
    }
}

namespace Pigeon.Messaging.Azure.ServiceBus.Tests;

using global::Azure;
using global::Azure.Messaging.ServiceBus;
using global::Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Options;
using NSubstitute;
using Pigeon.Messaging.Consuming.Configuration;
using Pigeon.Messaging.Producing;

public class ServiceBusTopologyFailureTests
{
    [Fact]
    public void Constructor_RequiresOptions()
        => Assert.Throws<ArgumentNullException>(() => new ServiceBusTopologyAdapter((IOptions<AzureServiceBusSettings>)null));

    [Fact]
    public void Constructor_RequiresAdminClient()
        => Assert.Throws<ArgumentNullException>(() => new ServiceBusTopologyAdapter((ServiceBusAdministrationClient)null));

    [Fact]
    public void Constructor_UsesConfiguredConnection()
    {
        var settings = Options.Create(new AzureServiceBusSettings { ConnectionString = "Endpoint=sb://test/;SharedAccessKeyName=Root;SharedAccessKey=abc" });
        Assert.Equal("AzureServiceBus", new ServiceBusTopologyAdapter(settings).BrokerName);
    }

    [Theory]
    [InlineData(409, true)]
    [InlineData(500, false)]
    public async Task Publish_HandlesRequestFailureByStatus(int status, bool ignored)
    {
        var client = Substitute.For<ServiceBusAdministrationClient>();
        client.CreateQueueAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Response<QueueProperties>>(new RequestFailedException(status, "failure")));
        var operation = () => new ServiceBusTopologyAdapter(client).EnsurePublishTopologyAsync(PublishingRoute.ForTopic("orders"));
        if (ignored) await operation();
        else await Assert.ThrowsAsync<RequestFailedException>(operation);
    }

    [Theory]
    [InlineData(ServiceBusFailureReason.MessagingEntityAlreadyExists, true)]
    [InlineData(ServiceBusFailureReason.ServiceCommunicationProblem, false)]
    public async Task Consume_HandlesServiceBusFailureByReason(ServiceBusFailureReason reason, bool ignored)
    {
        var client = Substitute.For<ServiceBusAdministrationClient>();
        client.CreateTopicAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Response<TopicProperties>>(new ServiceBusException("failure", reason)));
        var operation = () => new ServiceBusTopologyAdapter(client).EnsureConsumeTopologyAsync(new ConsumerEndpoint("orders", "billing"));
        if (ignored) await operation();
        else await Assert.ThrowsAsync<ServiceBusException>(operation);
    }

    [Fact]
    public async Task Consume_ForwardsCancellationTokenToBothOperations()
    {
        var client = Substitute.For<ServiceBusAdministrationClient>();
        using var source = new CancellationTokenSource();
        await new ServiceBusTopologyAdapter(client).EnsureConsumeTopologyAsync(new ConsumerEndpoint("orders", "billing"), source.Token);
        await client.Received(1).CreateTopicAsync("orders", source.Token);
        await client.Received(1).CreateSubscriptionAsync("orders", "billing", source.Token);
    }
}

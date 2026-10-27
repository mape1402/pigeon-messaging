namespace Pigeon.Messaging.Azure.EventGrid.Tests
{
    using global::Azure;
    using global::Azure.Messaging.ServiceBus;
    using global::Azure.Messaging.ServiceBus.Administration;
    using Microsoft.Extensions.Options;
    using NSubstitute;
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Producing;

    public class EventGridTopologyAdapterTests
    {
        [Fact]
        public void Constructor_RequiresClientFactory()
            => Assert.Throws<ArgumentNullException>(() => new EventGridTopologyAdapter((AzureEventGridClientFactory)null));

        [Fact]
        public void Constructor_RequiresAdministrationClientFactory()
            => Assert.Throws<ArgumentNullException>(() => new EventGridTopologyAdapter(CreateFactory(), null));

        [Fact]
        public void BrokerName_IdentifiesEventGrid()
            => Assert.Equal("AzureEventGrid", new EventGridTopologyAdapter(CreateFactory()).BrokerName);

        [Fact]
        public async Task EnsurePublishTopology_CompletesWithoutProvisioning()
            => await new EventGridTopologyAdapter(CreateFactory())
                .EnsurePublishTopologyAsync(PublishingRoute.ForTopic("orders"));

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public async Task EnsureConsumeTopology_WithoutEndpointDoesNotCreateClient(string connection)
        {
            var adapter = new EventGridTopologyAdapter(CreateFactory(new AzureEventGridSettings { ServiceBusEndPoint = connection }),
                () => throw new InvalidOperationException());
            await adapter.EnsureConsumeTopologyAsync(new ConsumerEndpoint("orders"));
        }

        [Fact]
        public async Task EnsureConsumeTopology_DefaultSubscriptionCreatesOnlyTopic()
        {
            var client = Substitute.For<ServiceBusAdministrationClient>();
            await CreateAdapter(client).EnsureConsumeTopologyAsync(new ConsumerEndpoint("orders"));
            await client.Received(1).CreateTopicAsync("orders", Arg.Any<CancellationToken>());
            await client.DidNotReceive().CreateSubscriptionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task EnsureConsumeTopology_ExplicitSubscriptionCreatesTopicAndSubscription()
        {
            var client = Substitute.For<ServiceBusAdministrationClient>();
            await CreateAdapter(client).EnsureConsumeTopologyAsync(new ConsumerEndpoint("orders", "billing"));
            await client.Received(1).CreateTopicAsync("orders", Arg.Any<CancellationToken>());
            await client.Received(1).CreateSubscriptionAsync("orders", "billing", Arg.Any<CancellationToken>());
        }

        [Theory]
        [InlineData(409, true)]
        [InlineData(500, false)]
        public async Task EnsureConsumeTopology_HandlesRequestFailuresByStatus(int status, bool ignored)
        {
            var client = Substitute.For<ServiceBusAdministrationClient>();
            client.CreateTopicAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromException<Response<TopicProperties>>(new RequestFailedException(status, "failure")));
            var operation = () => CreateAdapter(client).EnsureConsumeTopologyAsync(new ConsumerEndpoint("orders"));
            if (ignored) await operation();
            else await Assert.ThrowsAsync<RequestFailedException>(operation);
        }

        [Theory]
        [InlineData(ServiceBusFailureReason.MessagingEntityAlreadyExists, true)]
        [InlineData(ServiceBusFailureReason.ServiceCommunicationProblem, false)]
        public async Task EnsureConsumeTopology_HandlesServiceBusFailuresByReason(ServiceBusFailureReason reason, bool ignored)
        {
            var client = Substitute.For<ServiceBusAdministrationClient>();
            client.CreateTopicAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromException<Response<TopicProperties>>(new ServiceBusException("failure", reason)));
            var operation = () => CreateAdapter(client).EnsureConsumeTopologyAsync(new ConsumerEndpoint("orders"));
            if (ignored) await operation();
            else await Assert.ThrowsAsync<ServiceBusException>(operation);
        }

        private static EventGridTopologyAdapter CreateAdapter(ServiceBusAdministrationClient client)
            => new(CreateFactory(new AzureEventGridSettings { ServiceBusEndPoint = "connection" }), () => client);

        private static AzureEventGridClientFactory CreateFactory(AzureEventGridSettings settings = null)
            => new(Options.Create(settings ?? new AzureEventGridSettings()));
    }
}

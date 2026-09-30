namespace Pigeon.Messaging.Azure.ServiceBus.Tests
{
    using global::Azure;
    using global::Azure.Messaging.ServiceBus.Administration;
    using NSubstitute;
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Producing;

    public class ServiceBusTopologyAdapterTests
    {
        [Fact]
        public async Task EnsurePublishTopologyAsync_Should_Create_Queue_For_Direct_Topic_Publish()
        {
            var adminClient = CreateAdminClient();
            var adapter = new ServiceBusTopologyAdapter(adminClient);

            await adapter.EnsurePublishTopologyAsync(PublishingRoute.ForTopic("orders.created"));

            await adminClient.Received(1).CreateQueueAsync("orders.created", Arg.Any<CancellationToken>());
            await adminClient.DidNotReceive().CreateTopicAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task EnsurePublishTopologyAsync_Should_Create_Queue_For_Routed_Publish()
        {
            var adminClient = CreateAdminClient();
            var adapter = new ServiceBusTopologyAdapter(adminClient);

            await adapter.EnsurePublishTopologyAsync(PublishingRoute.ForExchange("events", "orders.created"));

            await adminClient.Received(1).CreateQueueAsync("orders.created", Arg.Any<CancellationToken>());
            await adminClient.DidNotReceive().CreateTopicAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task EnsureConsumeTopologyAsync_Should_Create_Queue_For_Default_Subscription()
        {
            var adminClient = CreateAdminClient();
            var adapter = new ServiceBusTopologyAdapter(adminClient);

            await adapter.EnsureConsumeTopologyAsync(new ConsumerEndpoint("orders.created"));

            await adminClient.Received(1).CreateQueueAsync("orders.created", Arg.Any<CancellationToken>());
            await adminClient.DidNotReceive().CreateTopicAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
            await adminClient.DidNotReceive().CreateSubscriptionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task EnsureConsumeTopologyAsync_Should_Create_Topic_Subscription_For_Explicit_Subscription()
        {
            var adminClient = CreateAdminClient();
            var adapter = new ServiceBusTopologyAdapter(adminClient);

            await adapter.EnsureConsumeTopologyAsync(new ConsumerEndpoint("orders.created", "billing"));

            await adminClient.Received(1).CreateTopicAsync("orders.created", Arg.Any<CancellationToken>());
            await adminClient.Received(1).CreateSubscriptionAsync("orders.created", "billing", Arg.Any<CancellationToken>());
            await adminClient.DidNotReceive().CreateQueueAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        private static ServiceBusAdministrationClient CreateAdminClient()
        {
            var adminClient = Substitute.For<ServiceBusAdministrationClient>();
            adminClient.CreateQueueAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<Response<QueueProperties>>(null));
            adminClient.CreateTopicAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<Response<TopicProperties>>(null));
            adminClient.CreateSubscriptionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<Response<SubscriptionProperties>>(null));

            return adminClient;
        }
    }
}

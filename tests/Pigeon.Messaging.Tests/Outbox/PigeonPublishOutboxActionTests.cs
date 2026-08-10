namespace Pigeon.Messaging.Tests.Outbox
{
    using Microsoft.Extensions.DependencyInjection;
    using Mule;
    using NSubstitute;
    using Pigeon.Messaging.Contracts;
    using Pigeon.Messaging.Outbox;
    using Pigeon.Messaging.Producing;
    using Pigeon.Messaging.Producing.Management;

    public class PigeonPublishOutboxActionTests
    {
        [Fact]
        public async Task ExecuteAsync_Should_Dispatch_Outbox_Message_Through_Producing_Manager()
        {
            var producingManager = Substitute.For<IProducingManager>();
            var action = new PigeonPublishOutboxAction(producingManager);
            var message = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Payload = "{}",
                PayloadType = typeof(PigeonPublishOutboxActionTestMessage).AssemblyQualifiedName,
                Topic = "orders.created",
                RoutingKey = "orders.created",
                CreatedOnUtc = DateTimeOffset.UtcNow
            };
            var durableAction = new DurableAction
            {
                Id = message.Id,
                Key = PigeonOutboxActionKeys.Publish,
                Payload = "{}",
                PayloadType = typeof(OutboxMessage).AssemblyQualifiedName,
                Status = DurableActionStatus.Pending,
                CreatedOnUtc = DateTimeOffset.UtcNow
            };
            var context = new MuleActionContext<OutboxMessage>(
                durableAction,
                new ServiceCollection().BuildServiceProvider(),
                message);

            await action.ExecuteAsync(context, CancellationToken.None);

            await producingManager.Received(1).PushOutboxAsync(message, Arg.Any<CancellationToken>());
        }
    }
}

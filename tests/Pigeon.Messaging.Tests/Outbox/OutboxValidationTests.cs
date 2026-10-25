namespace Pigeon.Messaging.Tests.Outbox;

using NSubstitute;
using Pigeon.Messaging;
using Pigeon.Messaging.Outbox;
using Pigeon.Messaging.Producing;
using Pigeon.Messaging.Producing.Management;

public class OutboxValidationTests
{
    [Fact]
    public void MuleRegistration_RequiresBuilder()
        => Assert.Throws<ArgumentNullException>(() => MuleOutboxConfigurationExtensions.AddPigeonOutboxAction(null, new OutboxSettings()));

    [Fact]
    public void Factory_RequiresSerializer()
        => Assert.Throws<ArgumentNullException>(() => new OutboxMessageFactory(null));

    [Fact]
    public void Factory_RequiresPayload()
        => Assert.Throws<ArgumentNullException>(() => new OutboxMessageFactory(Substitute.For<ISerializer>()).Create(null, PublishingRoute.ForTopic("orders"), false));

    [Fact]
    public void Factory_RequiresRoute()
        => Assert.Throws<ArgumentNullException>(() => new OutboxMessageFactory(Substitute.For<ISerializer>()).Create("message", null, false));

    [Fact]
    public void PublishAction_RequiresManager()
        => Assert.Throws<ArgumentNullException>(() => new PigeonPublishOutboxAction(null));

    [Fact]
    public async Task PublishAction_RequiresContext()
        => await Assert.ThrowsAsync<ArgumentNullException>(() => new PigeonPublishOutboxAction(Substitute.For<IProducingManager>()).ExecuteAsync(null, CancellationToken.None).AsTask());
}

using Mule;
using Mule.InMemory;
using NSubstitute;
using Pigeon.Messaging.Outbox;

namespace Pigeon.Messaging.Outbox.InMemory.Tests;

public class MuleInMemoryOutboxTests
{
    private readonly IInMemoryMule _mule = Substitute.For<IInMemoryMule>();
    private readonly IMuleSerializer _serializer = Substitute.For<IMuleSerializer>();

    [Fact]
    public void Constructor_RejectsNullDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new MuleInMemoryOutbox(null, _serializer));
        Assert.Throws<ArgumentNullException>(() => new MuleInMemoryOutbox(_mule, null));
    }

    [Theory]
    [InlineData(DurableActionStatus.Pending, OutboxMessageStatus.Pending)]
    [InlineData(DurableActionStatus.Locked, OutboxMessageStatus.Locked)]
    [InlineData(DurableActionStatus.Completed, OutboxMessageStatus.Published)]
    [InlineData(DurableActionStatus.Failed, OutboxMessageStatus.Failed)]
    public void Messages_MapsStatusAndDeliveryFields(DurableActionStatus status, OutboxMessageStatus expected)
    {
        var action = new DurableAction
        {
            Id = Guid.NewGuid(), Key = PigeonOutboxActionKeys.Publish, Payload = "{}", Status = status,
            Attempts = 2, LastError = "failure", CreatedOnUtc = DateTimeOffset.UtcNow
        };
        _mule.Actions.Returns(new[] { action });
        _serializer.Deserialize("{}", typeof(OutboxMessage)).Returns(new OutboxMessage());
        var message = Assert.Single(new MuleInMemoryOutbox(_mule, _serializer).Messages);

        Assert.Equal(expected, message.Status);
        Assert.Equal(action.Id, message.Id);
        Assert.Equal(2, message.Attempts);
        Assert.Equal("failure", message.LastError);
        Assert.Equal(action.CreatedOnUtc, message.CreatedOnUtc);
    }

    [Fact]
    public void Messages_IgnoresUnrelatedActions()
    {
        _mule.Actions.Returns(new[] { new DurableAction { Key = ActionKey.From("other") } });
        Assert.Empty(new MuleInMemoryOutbox(_mule, _serializer).Messages);
    }

    [Fact]
    public void Clear_ClearsMutableActions()
    {
        var actions = new List<DurableAction> { new() };
        _mule.Actions.Returns(actions);
        new MuleInMemoryOutbox(_mule, _serializer).Clear();
        Assert.Empty(actions);
    }

    [Fact]
    public void Clear_UsesMuleMethodWhenActionsAreReadOnly()
    {
        var mule = new MuleWithClear();
        new MuleInMemoryOutbox(mule, _serializer).Clear();
        Assert.True(mule.Cleared);
    }

    [Fact]
    public void Clear_WithoutCollectionOrClearMethodDoesNotThrow()
    {
        _mule.Actions.Returns(new ReadOnlyActions());
        new MuleInMemoryOutbox(_mule, _serializer).Clear();
    }
}

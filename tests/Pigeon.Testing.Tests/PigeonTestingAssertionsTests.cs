using NSubstitute;
using Pigeon.Messaging.Producing;

namespace Pigeon.Testing.Tests;

public class PigeonTestingAssertionsTests
{
    private readonly IPigeonTestingTransport _transport = Substitute.For<IPigeonTestingTransport>();

    [Fact]
    public void ShouldContainMessage_RejectsNullTransport()
        => Assert.Throws<ArgumentNullException>(() => PigeonTestingAssertions.ShouldContainMessage<string>(null));

    [Fact]
    public void ShouldContainMessage_RejectsNullPredicate()
        => Assert.Throws<ArgumentNullException>(() => _transport.ShouldContainMessage<string>(null));

    [Fact]
    public void ShouldContainMessage_ThrowsForMissingMessage()
        => Assert.Throws<PigeonTestingAssertionException>(() => _transport.ShouldContainMessage<string>());

    [Fact]
    public void ShouldContainMessage_ReturnsMatchingMessage()
    {
        _transport.PublishedMessages.Returns(new[] { Message("hello"), Message("world") });
        Assert.Equal("world", _transport.ShouldContainMessage<string>(x => x == "world").Message);
    }

    [Fact]
    public void ShouldContainMessage_IgnoresOtherMessageTypes()
    {
        _transport.PublishedMessages.Returns(new[] { Message(new object()), Message("hello") });
        Assert.Equal("hello", _transport.ShouldContainMessage<string>().Message);
    }

    [Fact]
    public void ShouldContainConsumedMessage_ThrowsForMissingMessage()
        => Assert.Throws<PigeonTestingAssertionException>(() => _transport.ShouldContainConsumedMessage<string>());

    [Fact]
    public void ShouldContainConsumedMessage_ReturnsMatchingMessage()
    {
        _transport.ConsumedMessages.Returns(new[] { Message("hello") });
        Assert.Equal("hello", _transport.ShouldContainConsumedMessage<string>(x => x == "hello").Message);
    }

    [Fact]
    public void ShouldContainConsumedMessage_IgnoresOtherMessageTypes()
    {
        _transport.ConsumedMessages.Returns(new[] { Message(new object()), Message("hello") });
        Assert.Equal("hello", _transport.ShouldContainConsumedMessage<string>().Message);
    }

    [Fact]
    public void ShouldContainDeadLetterMessage_ThrowsForMissingMessage()
        => Assert.Throws<PigeonTestingAssertionException>(() => _transport.ShouldContainDeadLetterMessage<string>());

    [Fact]
    public void ShouldContainDeadLetterMessage_ReturnsMatchingMessage()
    {
        _transport.DeadLetterMessages.Returns(new[] { Message("hello") });
        Assert.Equal("hello", _transport.ShouldContainDeadLetterMessage<string>(x => x == "hello").Message);
    }

    [Fact]
    public void ShouldContainDeadLetterMessage_IgnoresOtherMessageTypes()
    {
        _transport.DeadLetterMessages.Returns(new[] { Message(new object()), Message("hello") });
        Assert.Equal("hello", _transport.ShouldContainDeadLetterMessage<string>().Message);
    }

    [Fact]
    public void ShouldHaveConsumerFailure_ThrowsForMissingFailure()
        => Assert.Throws<PigeonTestingAssertionException>(() => _transport.ShouldHaveConsumerFailure<string>());

    [Fact]
    public void ShouldHaveConsumerFailure_ReturnsMatchingFailure()
    {
        var failure = new PigeonTestingFailure(Message("hello"), new InvalidOperationException(), PigeonTestingFailureKind.Consumer);
        _transport.ConsumerFailures.Returns(new[] { failure });
        Assert.Same(failure, _transport.ShouldHaveConsumerFailure<string>(x => x == "hello"));
    }

    [Fact]
    public void ShouldHaveConsumerFailure_IgnoresOtherMessageTypes()
    {
        var other = new PigeonTestingFailure(Message(new object()), new InvalidOperationException(), PigeonTestingFailureKind.Consumer);
        var expected = new PigeonTestingFailure(Message("hello"), new InvalidOperationException(), PigeonTestingFailureKind.Consumer);
        _transport.ConsumerFailures.Returns(new[] { other, expected });
        Assert.Same(expected, _transport.ShouldHaveConsumerFailure<string>());
    }

    [Fact]
    public void ShouldBe_ReturnsEqualValue() => Assert.Equal(42, 42.ShouldBe(42));

    [Fact]
    public void ShouldBe_ThrowsForDifferentValue() => Assert.Throws<PigeonTestingAssertionException>(() => 42.ShouldBe(43));

    private static PigeonTestingMessage Message(object value)
        => new(value, "test", PublishingRoute.ForTopic("test"), null, "{}", false, DateTimeOffset.UtcNow);
}

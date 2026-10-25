namespace Pigeon.Messaging.Tests.Consuming.Dispatching;

using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Pigeon.Messaging.Consuming.Configuration;
using Pigeon.Messaging.Consuming.Dispatching;
using Pigeon.Messaging.Contracts;
using System.Text;

public class PigeonConsumeEnvelopeFactoryTests
{
    private readonly ISerializer _serializer = Substitute.For<ISerializer>();

    [Fact]
    public void Constructor_RequiresSerializer()
        => Assert.Throws<ArgumentNullException>(() => new PigeonConsumeEnvelopeFactory(null));

    [Fact]
    public void Create_RequiresContext()
        => Assert.Throws<ArgumentNullException>(() => new PigeonConsumeEnvelopeFactory(_serializer).Create(null));

    [Fact]
    public void Create_CopiesPayloadAndMetadata()
    {
        _serializer.Serialize(Arg.Any<object>()).Returns("\"hello\"");
        var context = new ConsumeContext
        {
            Topic = "orders", Message = "hello", MessageType = typeof(string), MessageId = "id",
            Headers = new Dictionary<string, string> { ["trace"] = "123" },
            RawMetadata = new Dictionary<string, string> { ["source"] = "test" }
        };
        var envelope = new PigeonConsumeEnvelopeFactory(_serializer).Create(context);
        Assert.Equal("\"hello\"", Encoding.UTF8.GetString(envelope.Payload));
        Assert.Equal("123", envelope.Headers["TRACE"]);
        Assert.Equal("test", envelope.Metadata["SOURCE"]);
        Assert.Equal("id", envelope.MessageId);
    }

    [Fact]
    public void CreateContext_RequiresEnvelope()
        => Assert.Throws<ArgumentNullException>(() => new PigeonConsumeEnvelopeFactory(_serializer).CreateContext(null, Substitute.For<IServiceProvider>()));

    [Fact]
    public void CreateContext_RequiresServices()
        => Assert.Throws<ArgumentNullException>(() => new PigeonConsumeEnvelopeFactory(_serializer).CreateContext(new PigeonConsumeEnvelope(), null));

    [Fact]
    public void CreateContext_RejectsUnknownConsumer()
    {
        var configurator = Substitute.For<IConsumingConfigurator>();
        using var services = new ServiceCollection().AddSingleton(configurator).BuildServiceProvider();
        var envelope = new PigeonConsumeEnvelope { Topic = "orders", Version = new SemanticVersion(1, 0, 0) };
        Assert.Throws<InvalidOperationException>(() => new PigeonConsumeEnvelopeFactory(_serializer).CreateContext(envelope, services));
    }

    [Fact]
    public void CreateContext_DeserializesMissingPayloadAsEmptyText()
    {
        var version = SemanticVersion.Default;
        var config = new ConsumerConfiguration<string>((_, _) => Task.CompletedTask) { Topic = "orders", Version = version };
        var configurator = Substitute.For<IConsumingConfigurator>();
        configurator.GetConfiguration("orders", version, Arg.Any<string>()).Returns(config);
        using var services = new ServiceCollection().AddSingleton(configurator).BuildServiceProvider();
        var envelope = new PigeonConsumeEnvelope { Topic = "orders", Version = version, Payload = null };

        new PigeonConsumeEnvelopeFactory(_serializer).CreateContext(envelope, services);

        _serializer.Received(1).Deserialize("", typeof(string));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CreateContext_ResolvesConsumerAndRestoresDelivery(bool fallback)
    {
        var version = new SemanticVersion(1, 0, 0);
        var config = new ConsumerConfiguration<string>((_, _) => Task.CompletedTask) { Topic = "orders", Version = version, Subscription = "billing" };
        var configurator = Substitute.For<IConsumingConfigurator>();
        if (fallback) configurator.GetConfiguration("orders", version).Returns(config);
        else configurator.GetConfiguration("orders", version, "billing").Returns(config);
        using var services = new ServiceCollection().AddSingleton(configurator).BuildServiceProvider();
        _serializer.Deserialize("\"hello\"", typeof(string)).Returns("hello");
        var envelope = new PigeonConsumeEnvelope { Topic = "orders", Version = version, Subscription = "billing", Payload = Encoding.UTF8.GetBytes("\"hello\""), CorrelationId = "trace" };
        var result = new PigeonConsumeEnvelopeFactory(_serializer).CreateContext(envelope, services);
        Assert.Equal("hello", result.Message);
        Assert.Equal("trace", result.CorrelationId);
        Assert.Equal("billing", result.Subscription);
        Assert.Equal(ConsumeExecutionSource.DeferredReplay, result.ExecutionSource);
    }
}

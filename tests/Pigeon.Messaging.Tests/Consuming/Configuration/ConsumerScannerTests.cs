using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Pigeon.Messaging.Consuming.Configuration;
using Pigeon.Messaging.Consuming.Dispatching;
using Pigeon.Messaging.Contracts;

namespace Pigeon.Messaging.Tests.Consuming.Configuration;

public class ConsumerScannerTests
{
    private readonly IConsumingConfigurator _subConfigurator = Substitute.For<IConsumingConfigurator>();
    private readonly ServiceCollection _services = new();

    [Fact]
    public void Scan_ShouldRegisterValidConsumer()
    {
        // Arrange
        var scanner = new ConsumerScanner(_services, _subConfigurator);

        // Act
        scanner.ScanHubConsumers([typeof(SampleConsumer)]);

        // Assert: consumer registered in DI
        var descriptor = _services.SingleOrDefault(s => s.ServiceType == typeof(SampleConsumer));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);

        // Assert: AddConsumer called with correct topic, version, and handler
        _subConfigurator.Received(1).AddConsumer<SampleMessage>(
            "sample-topic",
            SemanticVersion.Parse("1.0.0"),
            Arg.Any<ConsumeHandler<SampleMessage>>());
    }

    [Fact]
    public void Scan_ShouldIgnoreAbstractConsumers()
    {
        // Arrange
        var scanner = new ConsumerScanner(_services, _subConfigurator);

        // Act
        scanner.ScanHubConsumers([typeof(AbstractConsumer)]);

        // Assert: abstract consumer NOT registered
        var descriptor = _services.SingleOrDefault(s => s.ServiceType == typeof(AbstractConsumer));
        Assert.Null(descriptor);

        _subConfigurator.DidNotReceiveWithAnyArgs().AddConsumer<SampleMessage>(
            default, default, default);
    }

    [Fact]
    public void Scan_ShouldThrow_WhenNoMessageParameter()
    {
        // Arrange
        var scanner = new ConsumerScanner(_services, _subConfigurator);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            scanner.ScanHubConsumers([typeof(InvalidConsumer)]));

        Assert.Contains("has an invalid signature", ex.Message);
    }

    [Fact]
    public void Scan_ShouldThrow_WhenInvalidMethodSignature()
    {
        // Arrange
        var scanner = new ConsumerScanner(_services, _subConfigurator);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            scanner.ScanHubConsumers([typeof(InvalidSignatureConsumer)]));

        Assert.Contains("has an invalid signature", ex.Message);
    }

    // ---------- Dummy consumers ----------
    private class SampleMessage { }

    private class SampleConsumer : HubConsumer
    {
        [Consumer("sample-topic", "1.0.0")]
        public Task Handle(SampleMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private abstract class AbstractConsumer : HubConsumer
    {
        [Consumer("abstract-topic", "1.0.0")]
        public Task Handle(SampleMessage message) => Task.CompletedTask;
    }

    private class InvalidConsumer : HubConsumer
    {
        [Consumer("bad-topic", "1.0.0")]
        public void BadHandler(CancellationToken cancellationToken) { }
    }

    private class InvalidSignatureConsumer : HubConsumer
    {
        [Consumer("bad-topic", "1.0.0")]
        public void BadHandler(SampleMessage message, CancellationToken cancellationToken) { }
    }
}

namespace Pigeon.Messaging.Tests.Producing;

using Microsoft.Extensions.Options;
using NSubstitute;
using Pigeon.Messaging.Contracts;
using Pigeon.Messaging.Producing;

public class PublishEnvelopeMetadataTests
{
    [Fact]
    public async Task Create_ConvertsMetadataValuesAndResolvesAlternateIdentifiers()
    {
        var context = CreateContext();
        context.AddMetadata("correlationId", 42);
        context.AddMetadata("traceId", "trace-1");

        var envelope = await CreateFactory().CreateAsync(context);

        Assert.Equal("42", envelope.CorrelationId);
        Assert.Equal("42", envelope.Metadata["correlationId"]);
        Assert.Equal("trace-1", envelope.TraceId);
    }

    [Fact]
    public async Task Create_ExplicitIdentifiersOverrideMetadata()
    {
        var context = CreateContext();
        context.CorrelationId = "explicit";
        context.AddMetadata("correlation-id", "metadata");

        var envelope = await CreateFactory().CreateAsync(context);

        Assert.Equal("explicit", envelope.CorrelationId);
        Assert.Equal("metadata", envelope.Metadata["correlation-id"]);
    }

    [Fact]
    public async Task Create_AllowsNullMetadataValues()
    {
        var context = CreateContext();
        context.AddMetadata<object>("correlation-id", null);

        var envelope = await CreateFactory().CreateAsync(context);

        Assert.Null(envelope.CorrelationId);
        Assert.Null(envelope.Metadata["correlation-id"]);
    }

    [Fact]
    public async Task Create_RawMessageWithoutTypeLeavesOperationUnset()
    {
        var context = new PublishContext
        {
            Message = "order", Route = PublishingRoute.ForTopic("orders"), Version = SemanticVersion.Default,
            IsRaw = true, Transport = "external", ContentType = "text/plain"
        };

        var envelope = await CreateFactory().CreateAsync(context);

        Assert.Null(envelope.Operation);
        Assert.Equal("external", envelope.Transport);
        Assert.Equal("text/plain", envelope.ContentType);
    }

    private static PublishContext CreateContext() => new()
    {
        Message = "order",
        MessageType = typeof(string),
        Route = PublishingRoute.ForTopic("orders"),
        Version = SemanticVersion.Default,
        IsRaw = true
    };

    private static PigeonPublishEnvelopeFactory CreateFactory()
    {
        var serializer = Substitute.For<ISerializer>();
        serializer.Serialize(Arg.Any<object>()).Returns("{}");
        return new PigeonPublishEnvelopeFactory(serializer, Options.Create(new GlobalSettings()));
    }
}

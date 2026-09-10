namespace Pigeon.Messaging.Tests.Producing
{
    using Microsoft.Extensions.Options;
    using NSubstitute;
    using Pigeon.Messaging.Contracts;
    using Pigeon.Messaging.Producing;
    using Pigeon.Messaging.Producing.Management;
    using System.Reflection;
    using System.Text;
    using System.Text.Json;

    public sealed class PigeonPublishEnvelopeTests
    {
        [Fact]
        public async Task Factory_Should_Create_Wrapped_Envelope_With_Metadata_Headers_And_Route()
        {
            var serializer = new TestSerializer();
            var factory = new PigeonPublishEnvelopeFactory(
                serializer,
                Options.Create(new GlobalSettings { Domain = "sales" }));
            var context = CreateContext(isRaw: false, PublishingRoute.ForExchange("events", "orders.created"));
            context.AddMetadata("tenant", "north");
            context.AddMetadata("correlation-id", "corr-1");
            context.AddHeader("x-trace-id", "trace-header");
            context.TraceId = "trace-1";

            var envelope = await factory.CreateAsync(context);

            Assert.False(envelope.IsRaw);
            Assert.Equal("pigeon", envelope.Transport);
            Assert.Equal("orders.created", envelope.Topic);
            Assert.Equal("1.2.0", envelope.Version);
            Assert.Equal("OrderMessage", envelope.Operation);
            Assert.Equal("events:orders.created", envelope.Destination);
            Assert.Equal("events", envelope.Exchange);
            Assert.Equal("orders.created", envelope.RoutingKey);
            Assert.Equal("north", envelope.Metadata["tenant"]);
            Assert.Equal("corr-1", envelope.CorrelationId);
            Assert.Equal("trace-1", envelope.TraceId);
            Assert.Equal("trace-header", envelope.Headers["x-trace-id"]);

            var payloadType = Type.GetType(envelope.PayloadType, throwOnError: true);
            var payload = serializer.Deserialize(
                Encoding.UTF8.GetString(envelope.Payload),
                payloadType);
            var wrapped = Assert.IsType<WrappedPayload<OrderMessage>>(payload);

            Assert.Equal("sales", wrapped.Domain);
            Assert.Equal("order-1", wrapped.Message.Id);
            Assert.Equal("north", wrapped.Metadata["tenant"].ToString());
        }

        [Fact]
        public async Task Factory_Should_Create_Raw_Envelope_Without_Wrapping_Message()
        {
            var serializer = new TestSerializer();
            var factory = new PigeonPublishEnvelopeFactory(
                serializer,
                Options.Create(new GlobalSettings { Domain = "sales" }));
            var context = CreateContext(isRaw: true, PublishingRoute.ForTopic("orders.raw"));

            var envelope = await factory.CreateAsync(context);

            Assert.True(envelope.IsRaw);
            Assert.Equal("orders.raw", envelope.Topic);
            Assert.Equal(typeof(OrderMessage).AssemblyQualifiedName, envelope.PayloadType);

            var message = Assert.IsType<OrderMessage>(serializer.Deserialize(
                Encoding.UTF8.GetString(envelope.Payload),
                typeof(OrderMessage)));
            Assert.Equal("order-1", message.Id);
        }

        [Fact]
        public async Task Invoker_Should_Publish_Wrapped_Envelope_Without_Producer_Interceptors()
        {
            var serializer = new TestSerializer();
            var factory = new PigeonPublishEnvelopeFactory(
                serializer,
                Options.Create(new GlobalSettings { Domain = "sales" }));
            var manager = Substitute.For<IProducingManager>();
            var invoker = new PigeonPublisherInvoker(manager, serializer);
            var context = CreateContext(isRaw: false, PublishingRoute.ForTopic("orders"));
            var envelope = await factory.CreateAsync(context);

            await invoker.PublishAsync(envelope);

            await manager.Received(1).PushAsync(
                Arg.Is<WrappedPayload<OrderMessage>>(payload => payload.Message.Id == "order-1"),
                Arg.Is<PublishingRoute>(route => route.Topic == "orders"),
                CancellationToken.None);
        }

        [Fact]
        public async Task Invoker_Should_Publish_Raw_Envelope()
        {
            var serializer = new TestSerializer();
            var factory = new PigeonPublishEnvelopeFactory(
                serializer,
                Options.Create(new GlobalSettings { Domain = "sales" }));
            var manager = Substitute.For<IProducingManager>();
            var invoker = new PigeonPublisherInvoker(manager, serializer);
            var context = CreateContext(isRaw: true, PublishingRoute.ForTopic("orders.raw"));
            var envelope = await factory.CreateAsync(context);

            await invoker.PublishAsync(envelope);

            await manager.Received(1).PushRawAsync(
                Arg.Is<OrderMessage>(message => message.Id == "order-1"),
                Arg.Is<PublishingRoute>(route => route.Topic == "orders.raw"),
                CancellationToken.None);
        }

        private static PublishContext CreateContext(bool isRaw, PublishingRoute route)
        {
            var context = new PublishContext
            {
                IsRaw = isRaw,
                Message = new OrderMessage("order-1"),
                MessageType = typeof(OrderMessage),
                Version = SemanticVersion.Parse("1.2.0")
            };

            typeof(PublishContext)
                .GetProperty(nameof(PublishContext.Route), BindingFlags.Instance | BindingFlags.Public)!
                .SetValue(context, route);

            return context;
        }

        private sealed record OrderMessage(string Id);

        private sealed class TestSerializer : ISerializer
        {
            private readonly JsonSerializerOptions _options = new()
            {
                Converters = { new SemanticVersionJsonConverter() }
            };

            public string Serialize(object payload)
                => JsonSerializer.Serialize(payload, payload.GetType(), _options);

            public object Deserialize(string rawJson, Type targetType)
                => JsonSerializer.Deserialize(rawJson, targetType, _options);
        }
    }
}

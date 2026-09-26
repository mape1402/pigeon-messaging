namespace Pigeon.Messaging.Kafka.Tests.Producing
{
    using Confluent.Kafka;
    using Pigeon.Messaging;
    using Pigeon.Messaging.Kafka.Producing;
    using PigeonSerializer = Pigeon.Messaging.ISerializer;

    public class KafkaJsonSerializerTests
    {
        [Fact]
        public void Constructor_Should_Throw_WhenSerializerIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new JsonSerializer<SampleMessage>(null));
        }

        [Fact]
        public void Serialize_Should_Delegate_ToPigeonSerializer()
        {
            var serializer = new TestSerializer("""{"Name":"pigeon"}""");
            var kafkaSerializer = new JsonSerializer<SampleMessage>(serializer);
            var message = new SampleMessage { Name = "pigeon" };

            var bytes = kafkaSerializer.Serialize(
                message,
                new SerializationContext(MessageComponentType.Value, "orders.created"));

            Assert.Equal("""{"Name":"pigeon"}""", bytes.FromBytes());
            Assert.Same(message, serializer.LastPayload);
        }

        private sealed class SampleMessage
        {
            public string Name { get; set; }
        }

        private sealed class TestSerializer : PigeonSerializer
        {
            private readonly string _json;

            public TestSerializer(string json)
            {
                _json = json;
            }

            public object LastPayload { get; private set; }

            public string Serialize(object payload)
            {
                LastPayload = payload;
                return _json;
            }

            public object Deserialize(string rawJson, Type targetType)
                => throw new NotSupportedException();
        }
    }
}

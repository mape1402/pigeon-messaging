namespace Pigeon.Messaging.Tests.Extensions
{
    using Pigeon.Messaging;
    using System.Text;

    public class SerializerExtensionsTests
    {
        [Fact]
        public void SerializeAsBytes_Should_EncodeSerializedPayloadAsUtf8()
        {
            var serializer = new TestSerializer("""{"Name":"pigeon"}""");

            var bytes = serializer.SerializeAsBytes(new { Name = "pigeon" });

            Assert.Equal("""{"Name":"pigeon"}""", Encoding.UTF8.GetString(bytes));
            Assert.NotNull(serializer.LastPayload);
        }

        [Fact]
        public void FromBytes_Should_DecodeUtf8Bytes()
        {
            var bytes = Encoding.UTF8.GetBytes("""{"Name":"pigeon"}""");

            var json = bytes.FromBytes();

            Assert.Equal("""{"Name":"pigeon"}""", json);
        }

        private sealed class TestSerializer : ISerializer
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

using Pigeon.Messaging.Consuming;
using Pigeon.Messaging.Contracts;
using System.Text.Json;

namespace Pigeon.Messaging.Tests.Consuming
{
    public class RawPayloadTests
    {
        private const string ValidJson = @"{
            ""Domain"": ""test-domain"",
            ""MessageVersion"": ""1.2.3"",
            ""CreatedOnUtc"": ""2024-01-01T00:00:00Z"",
            ""Message"": { ""Text"": ""Hello"" },
            ""Metadata"": { ""Key"": { ""Prop"": ""Value"" } }
        }";

        [Fact]
        public void Constructor_Should_Parse_Valid_Json()
        {
            var payload = new RawPayload(ValidJson);
            Assert.Equal("test-domain", payload.Domain);
            Assert.Equal(new SemanticVersion(1, 2, 3), payload.MessageVersion);
            Assert.Equal(DateTimeOffset.Parse("2024-01-01T00:00:00Z"), payload.CreatedOnUtc);
        }

        [Fact]
        public void Constructor_Should_Throw_If_Missing_Field()
        {
            var invalidJson = @"{ ""MessageVersion"": ""1.0.0"", ""CreatedOnUtc"": ""2024-01-01T00:00:00Z"" }";
            Assert.Throws<JsonException>(() => new RawPayload(invalidJson));
        }

        [Fact]
        public void GetMessage_Should_Deserialize_Message()
        {
            var payload = new RawPayload(ValidJson);
            var result = payload.GetMessage(typeof(Message));

            Assert.IsType<Message>(result);
            Assert.Equal("Hello", ((Message)result).Text);
        }

        [Fact]
        public void GetMetadata_Should_Return_Metadata()
        {
            var payload = new RawPayload(ValidJson);
            var meta = payload.GetMetadata();

            Assert.True(meta.ContainsKey("Key"));
            Assert.Equal(@"{ ""Prop"": ""Value"" }", meta["Key"]);
        }

        private class Message
        {
            public string Text { get; set; }
        }
    }
}

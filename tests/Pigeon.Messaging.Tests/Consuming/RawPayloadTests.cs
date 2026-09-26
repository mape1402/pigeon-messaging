using NSubstitute;
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

        private const string CamelCaseJson = @"{
            ""$pigeon"": { ""formatVersion"": ""1.0"", ""propertyNamingPolicy"": ""CamelCase"" },
            ""domain"": ""test-domain"",
            ""messageVersion"": ""1.2.3"",
            ""createdOnUtc"": ""2024-01-01T00:00:00Z"",
            ""message"": { ""text"": ""Hello"" },
            ""metadata"": { ""Key"": { ""Prop"": ""Value"" } }
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
            var serializer = Substitute.For<ISerializer>();
            serializer.Deserialize(Arg.Any<string>(), typeof(Message)).Returns(x => JsonSerializer.Deserialize<Message>((string)x[0]));
            var result = payload.GetMessage(typeof(Message), serializer);

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

        [Fact]
        public void Factory_Should_Read_Default_Wrapped_Payload_With_Format_Marker()
        {
            var json = @"{
                ""$pigeon"": { ""formatVersion"": ""1.0"", ""propertyNamingPolicy"": ""Default"" },
                ""Domain"": ""test-domain"",
                ""MessageVersion"": ""1.2.3"",
                ""CreatedOnUtc"": ""2024-01-01T00:00:00Z"",
                ""Message"": { ""Text"": ""Hello"" },
                ""Metadata"": { ""Key"": { ""Prop"": ""Value"" } }
            }";
            var factory = CreateFactory();

            var payload = factory.Create(json);

            Assert.Equal("test-domain", payload.Domain);
            Assert.Equal(new SemanticVersion(1, 2, 3), payload.MessageVersion);
            Assert.True(payload.GetMetadata().ContainsKey("Key"));
        }

        [Fact]
        public void Factory_Should_Read_CamelCase_Wrapped_Payload_With_Format_Marker()
        {
            var factory = CreateFactory(JsonNamingPolicy.CamelCase);
            var serializer = new DefaultSerializer(new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var payload = factory.Create(CamelCaseJson);
            var message = Assert.IsType<Message>(payload.GetMessage(typeof(Message), serializer));

            Assert.Equal("test-domain", payload.Domain);
            Assert.Equal("Hello", message.Text);
            Assert.True(payload.GetMetadata().ContainsKey("Key"));
        }

        [Fact]
        public void Factory_Should_Read_Legacy_Default_Wrapped_Payload_When_Local_Policy_Is_Default()
        {
            var factory = CreateFactory();

            var payload = factory.Create(ValidJson);

            Assert.Equal("test-domain", payload.Domain);
        }

        [Fact]
        public void Factory_Should_Read_Legacy_CamelCase_Wrapped_Payload_When_Local_Policy_Is_CamelCase()
        {
            var legacyCamelCaseJson = @"{
                ""domain"": ""test-domain"",
                ""messageVersion"": ""1.2.3"",
                ""createdOnUtc"": ""2024-01-01T00:00:00Z"",
                ""message"": { ""text"": ""Hello"" },
                ""metadata"": { ""Key"": { ""Prop"": ""Value"" } }
            }";
            var factory = CreateFactory(JsonNamingPolicy.CamelCase);

            var payload = factory.Create(legacyCamelCaseJson);

            Assert.Equal("test-domain", payload.Domain);
        }

        [Fact]
        public void Factory_Should_Throw_Mismatch_When_Declared_Policy_Differs_From_Local_Policy()
        {
            var factory = CreateFactory();

            var exception = Assert.Throws<WrappedPayloadJsonPolicyMismatchException>(() => factory.Create(CamelCaseJson));

            Assert.Equal(WrappedPayloadJsonPolicyNames.Default, exception.ExpectedPolicy);
            Assert.Equal(WrappedPayloadJsonPolicyNames.CamelCase, exception.ActualPolicy);
            Assert.False(exception.IsLegacyPayload);
        }

        [Fact]
        public void Factory_Should_Throw_Mismatch_When_Legacy_Policy_Differs_From_Local_Policy()
        {
            var factory = CreateFactory(JsonNamingPolicy.CamelCase);

            var exception = Assert.Throws<WrappedPayloadJsonPolicyMismatchException>(() => factory.Create(ValidJson));

            Assert.Equal(WrappedPayloadJsonPolicyNames.CamelCase, exception.ExpectedPolicy);
            Assert.Equal(WrappedPayloadJsonPolicyNames.Default, exception.ActualPolicy);
            Assert.True(exception.IsLegacyPayload);
        }

        [Fact]
        public void Factory_Should_Throw_Clear_Error_For_Mixed_Legacy_Wrapper()
        {
            var json = @"{
                ""Domain"": ""test-domain"",
                ""messageVersion"": ""1.2.3"",
                ""CreatedOnUtc"": ""2024-01-01T00:00:00Z"",
                ""message"": { ""text"": ""Hello"" }
            }";
            var factory = CreateFactory();

            var exception = Assert.Throws<JsonException>(() => factory.Create(json));

            Assert.Contains("partial or mixed wrapper shape", exception.Message);
        }

        private static RawPayloadFactory CreateFactory(JsonNamingPolicy namingPolicy = null)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = namingPolicy,
                Converters = { new SemanticVersionJsonConverter() }
            };

            return new RawPayloadFactory(new WrappedPayloadJsonPolicyProvider(options));
        }

        private class Message
        {
            public string Text { get; set; }
        }
    }
}

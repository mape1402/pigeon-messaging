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
        public void Constructor_Should_Throw_If_Json_Is_Null()
        {
            Assert.Throws<ArgumentNullException>(() => new RawPayload(null));
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
        public void GetMessage_Should_Throw_When_MessageType_Is_Null()
        {
            var payload = new RawPayload(ValidJson);
            var serializer = Substitute.For<ISerializer>();

            Assert.Throws<ArgumentNullException>(() => payload.GetMessage(null, serializer));
        }

        [Fact]
        public void GetMessage_Should_Throw_When_Serializer_Is_Null()
        {
            var payload = new RawPayload(ValidJson);

            Assert.Throws<ArgumentNullException>(() => payload.GetMessage(typeof(Message), null));
        }

        [Fact]
        public void GetMessage_Should_Throw_When_Message_Property_Is_Missing()
        {
            var json = @"{
                ""Domain"": ""test-domain"",
                ""MessageVersion"": ""1.2.3"",
                ""CreatedOnUtc"": ""2024-01-01T00:00:00Z""
            }";
            var payload = new RawPayload(json);
            var serializer = Substitute.For<ISerializer>();

            var exception = Assert.Throws<JsonException>(() => payload.GetMessage(typeof(Message), serializer));

            Assert.Contains("Missing 'Message'", exception.Message);
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
        public void GetMetadata_Should_Return_Empty_Dictionary_When_Metadata_Is_Missing()
        {
            var json = @"{
                ""Domain"": ""test-domain"",
                ""MessageVersion"": ""1.2.3"",
                ""CreatedOnUtc"": ""2024-01-01T00:00:00Z"",
                ""Message"": { ""Text"": ""Hello"" }
            }";
            var payload = new RawPayload(json);

            var metadata = payload.GetMetadata();

            Assert.Empty(metadata);
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
        public void Factory_Should_Throw_Mismatch_When_Legacy_CamelCase_Differs_From_Local_Policy()
        {
            var legacyCamelCaseJson = @"{
                ""domain"": ""test-domain"",
                ""messageVersion"": ""1.2.3"",
                ""createdOnUtc"": ""2024-01-01T00:00:00Z"",
                ""message"": { ""text"": ""Hello"" }
            }";
            var factory = CreateFactory();

            var exception = Assert.Throws<WrappedPayloadJsonPolicyMismatchException>(() => factory.Create(legacyCamelCaseJson));

            Assert.Equal(WrappedPayloadJsonPolicyNames.Default, exception.ExpectedPolicy);
            Assert.Equal(WrappedPayloadJsonPolicyNames.CamelCase, exception.ActualPolicy);
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

        [Fact]
        public void Factory_Should_Throw_Clear_Error_For_Unrecognized_Wrapper()
        {
            var factory = CreateFactory();

            var exception = Assert.Throws<JsonException>(() => factory.Create(@"{ ""value"": 123 }"));

            Assert.Contains("does not contain a recognized Pigeon wrapper shape", exception.Message);
        }

        [Fact]
        public void Factory_Should_Throw_When_Json_Is_Null()
        {
            var factory = CreateFactory();

            Assert.Throws<ArgumentNullException>(() => factory.Create(null));
        }

        [Fact]
        public void Factory_Should_Throw_When_PolicyProvider_Is_Null()
        {
            Assert.Throws<ArgumentNullException>(() => new RawPayloadFactory(null));
        }

        [Fact]
        public void Factory_Should_Throw_When_Pigeon_Marker_Is_Not_Object()
        {
            var json = @"{
                ""$pigeon"": ""invalid"",
                ""Domain"": ""test-domain"",
                ""MessageVersion"": ""1.2.3"",
                ""CreatedOnUtc"": ""2024-01-01T00:00:00Z"",
                ""Message"": { ""Text"": ""Hello"" }
            }";
            var factory = CreateFactory();

            var exception = Assert.Throws<JsonException>(() => factory.Create(json));

            Assert.Contains("Invalid '$pigeon' property", exception.Message);
        }

        [Fact]
        public void Factory_Should_Throw_When_FormatVersion_Is_Unsupported()
        {
            var json = @"{
                ""$pigeon"": { ""formatVersion"": ""2.0"", ""propertyNamingPolicy"": ""Default"" },
                ""Domain"": ""test-domain"",
                ""MessageVersion"": ""1.2.3"",
                ""CreatedOnUtc"": ""2024-01-01T00:00:00Z"",
                ""Message"": { ""Text"": ""Hello"" }
            }";
            var factory = CreateFactory();

            var exception = Assert.Throws<JsonException>(() => factory.Create(json));

            Assert.Contains("Unsupported WrappedPayload format version", exception.Message);
        }

        [Fact]
        public void Factory_Should_Throw_When_PropertyNamingPolicy_Is_Missing()
        {
            var json = @"{
                ""$pigeon"": { ""formatVersion"": ""1.0"" },
                ""Domain"": ""test-domain"",
                ""MessageVersion"": ""1.2.3"",
                ""CreatedOnUtc"": ""2024-01-01T00:00:00Z"",
                ""Message"": { ""Text"": ""Hello"" }
            }";
            var factory = CreateFactory();

            var exception = Assert.Throws<JsonException>(() => factory.Create(json));

            Assert.Contains("Invalid '$pigeon.propertyNamingPolicy'", exception.Message);
        }

        [Fact]
        public void PolicyProvider_Should_Resolve_Custom_Naming_Policy()
        {
            var provider = new WrappedPayloadJsonPolicyProvider(new JsonSerializerOptions
            {
                PropertyNamingPolicy = new UpperCaseNamingPolicy()
            });

            Assert.Equal(typeof(UpperCaseNamingPolicy).FullName, provider.PolicyName);
            Assert.Equal(typeof(UpperCaseNamingPolicy).FullName, provider.SerializationInfo.PropertyNamingPolicy);
        }

        [Fact]
        public void PolicyProvider_Should_Throw_When_Options_Are_Null()
        {
            Assert.Throws<ArgumentNullException>(() => new WrappedPayloadJsonPolicyProvider(null));
        }

        [Fact]
        public void PolicyMismatchException_Should_Create_Message_Without_Property_Name()
        {
            var exception = new WrappedPayloadJsonPolicyMismatchException("Default", "CamelCase");

            Assert.Equal("Default", exception.ExpectedPolicy);
            Assert.Equal("CamelCase", exception.ActualPolicy);
            Assert.Null(exception.PropertyName);
            Assert.DoesNotContain("Property '", exception.Message);
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

        private sealed class UpperCaseNamingPolicy : JsonNamingPolicy
        {
            public override string ConvertName(string name)
                => name.ToUpperInvariant();
        }
    }
}

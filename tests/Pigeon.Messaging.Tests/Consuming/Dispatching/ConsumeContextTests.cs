namespace Pigeon.Messaging.Tests.Consuming.Dispatching
{
    using Pigeon.Messaging.Consuming.Dispatching;
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using Xunit;

    public class ConsumeContextTests
    {
        [Fact]
        public void GetMetadata_ShouldThrow_KeyNotFoundException_WhenKeyMissing()
        {
            var context = new ConsumeContext
            {
                RawMetadata = new Dictionary<string, string>()
            };

            Assert.Throws<KeyNotFoundException>(() => context.GetMetadata<int>("missing-key"));
        }

        [Fact]
        public void GetMetadata_ShouldThrow_InvalidCastException_WhenValueCannotBeCast()
        {
            var context = new ConsumeContext
            {
                RawMetadata = new Dictionary<string, string>()
                {
                    ["key"] = "\"not an int\""
                }
            };

            Assert.Throws<JsonException>(() => context.GetMetadata<int>("key"));
        }

        [Fact]
        public void GetMetadata_ShouldDeserializeAndCacheValue()
        {
            var context = new ConsumeContext
            {
                RawMetadata = new Dictionary<string, string>()
                {
                    ["key"] = "123"
                }
            };

            int value = context.GetMetadata<int>("key");
            Assert.Equal(123, value);

            // Call again should get from cache, no exception
            int cachedValue = context.GetMetadata<int>("key");
            Assert.Equal(123, cachedValue);
        }

        [Fact]
        public void GetMetadata_ShouldDeserializeComplexObject()
        {
            var obj = new { Name = "Test", Count = 42 };
            var json = JsonSerializer.Serialize(obj);

            var context = new ConsumeContext
            {
                RawMetadata = new Dictionary<string, string>()
                {
                    ["complex"] = json
                }
            };

            var result = context.GetMetadata<Dictionary<string, JsonElement>>("complex");

            Assert.Equal("Test", result["Name"].GetString());
            Assert.Equal(42, result["Count"].GetInt32());
        }
    }

}

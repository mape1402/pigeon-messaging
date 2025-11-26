namespace Pigeon.Messaging.Tests.Consuming.Dispatching
{
    using NSubstitute;
    using Pigeon.Messaging.Consuming;
    using Pigeon.Messaging.Consuming.Dispatching;
    using Pigeon.Messaging.Contracts;
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
            var name = "Test";
            var count = 42;

            var obj = new ComplexMetadata { Name = name, Count = count };
            var json = JsonSerializer.Serialize(obj);

            var serializer = Substitute.For<ISerializer>();
            serializer.Deserialize(Arg.Any<string>(), Arg.Any<Type>()).Returns(null);

            var serviceProvider = Substitute.For<IServiceProvider>();
            serviceProvider.GetService(Arg.Any<Type>()).Returns(serializer);

            var context = new ConsumeContext
            {
                Services = serviceProvider,
                RawMetadata = new Dictionary<string, string>()
                {
                    ["key"] = json
                }
            };

            Assert.Throws<InvalidCastException>(() => context.GetMetadata<int>("key"));
        }

        [Fact]
        public void GetMetadata_ShouldDeserializeAndCacheValue()
        {
            var serializer = Substitute.For<ISerializer>();
            serializer.Deserialize(Arg.Any<string>(), Arg.Any<Type>()).Returns(123);

            var serviceProvider = Substitute.For<IServiceProvider>();   
            serviceProvider.GetService(Arg.Any<Type>()).Returns(serializer);

            var context = new ConsumeContext
            {
                Services = serviceProvider,
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
            var name = "Test";
            var count = 42;

            var obj = new ComplexMetadata { Name = name, Count = count };
            var json = JsonSerializer.Serialize(obj);

            var serializer = Substitute.For<ISerializer>();
            serializer.Deserialize(Arg.Any<string>(), Arg.Any<Type>()).Returns(obj);

            var serviceProvider = Substitute.For<IServiceProvider>();
            serviceProvider.GetService(Arg.Any<Type>()).Returns(serializer);

            var context = new ConsumeContext
            {
                Services = serviceProvider,
                RawMetadata = new Dictionary<string, string>()
                {
                    ["complex"] = json
                }
            };

            var result = context.GetMetadata<ComplexMetadata>("complex");

            Assert.Equal(name, result.Name);
            Assert.Equal(count, result.Count);
        }


        [Fact]
        public void GetMetadata_ShouldDeserializeComplexObject2()
        {
            var rawPayload = new RawPayload(json);

            var jsonOptions = new JsonSerializerOptions
            {
                Converters = { new SemanticVersionJsonConverter() },
            };

            var serializer = new DefaultSerializer(jsonOptions);

            var serviceProvider = Substitute.For<IServiceProvider>();
            serviceProvider.GetService(Arg.Any<Type>()).Returns(serializer);

            var context = new ConsumeContext
            {
                Services = serviceProvider,
                RawMetadata = rawPayload.GetMetadata()
            };

            var result = context.GetMetadata<ComplexMetadata>("Metadata");

            Assert.Equal("demo", result.Name);
            Assert.Equal(1, result.Count);
        }

        private string json =
            @"{
                ""Domain"":""Test"",
                ""MessageVersion"":""1.0.0"",
                ""CreatedOnUtc"":""2025-08-12T02:04:06.4751931+00:00"",
                ""Message"":{
                    ""EventId"":""01K2E0A7NA956VVW29M0XH827T"",
                    ""Source"":""Demo"",
                    ""CreatedOn"":""2025-08-12T02:03:57.9946041\u002B00:00"",
                    ""CorrelationId"":null
                },
                ""Metadata"":{
                    ""Metadata"":{
                        ""Name"":""demo"",
                        ""Count"":1
                    }
                }
            }";
    }

    public class ComplexMetadata
    {
        public string Name { get; set; }

        public int Count { get; set; }
    }
}

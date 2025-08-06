namespace Pigeon.Messaging.Tests.Consuming.Dispatching
{
    using NSubstitute;
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
    }

    public class ComplexMetadata
    {
        public string Name { get; set; }

        public int Count { get; set; }
    }
}

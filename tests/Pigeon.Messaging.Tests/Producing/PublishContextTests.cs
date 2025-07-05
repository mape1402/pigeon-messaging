namespace Pigeon.Messaging.Tests.Producing
{
    using Pigeon.Messaging.Producing;
    using System;
    using System.Collections.Generic;
    using Xunit;

    public class PublishContextTests
    {
        [Fact]
        public void AddMetadata_Should_AddEntry_WhenKeyIsNew()
        {
            var context = new PublishContext();

            context.AddMetadata("key1", 123);

            var metadata = context.GetMetadata();

            Assert.True(metadata.ContainsKey("key1"));
            Assert.Equal(123, metadata["key1"]);
        }

        [Fact]
        public void AddMetadata_Should_ThrowArgumentNullException_WhenKeyIsNullOrWhitespace()
        {
            var context = new PublishContext();

            Assert.Throws<ArgumentNullException>(() => context.AddMetadata<int>(null, 1));
            Assert.Throws<ArgumentNullException>(() => context.AddMetadata<int>("", 1));
            Assert.Throws<ArgumentNullException>(() => context.AddMetadata<int>("   ", 1));
        }

        [Fact]
        public void AddMetadata_Should_ThrowInvalidOperationException_WhenKeyAlreadyExists()
        {
            var context = new PublishContext();
            context.AddMetadata("key1", "value1");

            var ex = Assert.Throws<InvalidOperationException>(() => context.AddMetadata("key1", "value2"));
            Assert.Contains("key 'key1' already exists", ex.Message);
        }

        [Fact]
        public void GetMetadata_ShouldReturnReadOnlyDictionary()
        {
            var context = new PublishContext();
            context.AddMetadata("key1", "value1");

            var metadata = context.GetMetadata();

            Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(metadata);

            // Ensure returned dictionary has expected data
            Assert.True(metadata.ContainsKey("key1"));
            Assert.Equal("value1", metadata["key1"]);

            // Ensure it's read-only: modification throws
            var readOnlyDict = Assert.IsType<System.Collections.ObjectModel.ReadOnlyDictionary<string, object>>(metadata);
            Assert.Throws<NotSupportedException>(() => ((IDictionary<string, object>)readOnlyDict).Add("newKey", "newValue"));
        }
    }
}

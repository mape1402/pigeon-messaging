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

            var metadata = context.Metadata;

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

            var metadata = context.Metadata;

            Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(metadata);

            // Ensure returned dictionary has expected data
            Assert.True(metadata.ContainsKey("key1"));
            Assert.Equal("value1", metadata["key1"]);

            // Ensure it's read-only: modification throws
            var readOnlyDict = Assert.IsType<System.Collections.ObjectModel.ReadOnlyDictionary<string, object>>(metadata);
            Assert.Throws<NotSupportedException>(() => ((IDictionary<string, object>)readOnlyDict).Add("newKey", "newValue"));
        }

        [Fact]
        public void AddHeader_Should_AddEntry_WhenKeyIsNew()
        {
            var context = new PublishContext();

            context.AddHeader("x-correlation-id", "corr-1");

            Assert.Equal("corr-1", context.Headers["x-correlation-id"]);
        }

        [Fact]
        public void AddHeader_Should_ThrowArgumentNullException_WhenKeyIsNullOrWhitespace()
        {
            var context = new PublishContext();

            Assert.Throws<ArgumentNullException>(() => context.AddHeader(null, "value"));
            Assert.Throws<ArgumentNullException>(() => context.AddHeader("", "value"));
            Assert.Throws<ArgumentNullException>(() => context.AddHeader("   ", "value"));
        }

        [Fact]
        public void AddHeader_Should_ThrowInvalidOperationException_WhenKeyAlreadyExists()
        {
            var context = new PublishContext();
            context.AddHeader("x-correlation-id", "corr-1");

            var ex = Assert.Throws<InvalidOperationException>(() => context.AddHeader("x-correlation-id", "corr-2"));
            Assert.Contains("Header with key 'x-correlation-id' already exists", ex.Message);
        }

        [Fact]
        public void Headers_ShouldReturnReadOnlyDictionary()
        {
            var context = new PublishContext();
            context.AddHeader("x-correlation-id", "corr-1");

            var headers = context.Headers;

            Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(headers);
            Assert.Equal("corr-1", headers["x-correlation-id"]);

            var readOnlyDict = Assert.IsType<System.Collections.ObjectModel.ReadOnlyDictionary<string, string>>(headers);
            Assert.Throws<NotSupportedException>(() => ((IDictionary<string, string>)readOnlyDict).Add("newKey", "newValue"));
        }

        [Fact]
        public void MergeMetadata_Should_IgnoreNullMetadata()
        {
            var context = new PublishContext();
            context.AddMetadata("key1", "value1");

            context.MergeMetadata(null);

            Assert.Equal("value1", context.Metadata["key1"]);
        }

        [Fact]
        public void MergeMetadata_Should_MergeAndReplaceMetadata()
        {
            var context = new PublishContext();
            context.AddMetadata("key1", "value1");

            context.MergeMetadata(new Dictionary<string, object>
            {
                ["key1"] = "value2",
                ["key2"] = 42
            });

            Assert.Equal("value2", context.Metadata["key1"]);
            Assert.Equal(42, context.Metadata["key2"]);
        }

        [Fact]
        public void MergeHeaders_Should_IgnoreNullHeaders()
        {
            var context = new PublishContext();
            context.AddHeader("x-correlation-id", "corr-1");

            context.MergeHeaders(null);

            Assert.Equal("corr-1", context.Headers["x-correlation-id"]);
        }

        [Fact]
        public void MergeHeaders_Should_MergeAndReplaceHeaders()
        {
            var context = new PublishContext();
            context.AddHeader("x-correlation-id", "corr-1");

            context.MergeHeaders(new Dictionary<string, string>
            {
                ["x-correlation-id"] = "corr-2",
                ["x-trace-id"] = "trace-1"
            });

            Assert.Equal("corr-2", context.Headers["x-correlation-id"]);
            Assert.Equal("trace-1", context.Headers["x-trace-id"]);
        }
    }
}

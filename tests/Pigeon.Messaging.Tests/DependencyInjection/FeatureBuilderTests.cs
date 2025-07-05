namespace Pigeon.Messaging.Tests.DependencyInjection
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using NSubstitute;
    using Pigeon.Messaging;
    using System.Collections.Generic;
    using Xunit;

    public class FeatureBuilderTests
    {
        [Fact]
        public void TryGetAdapterSettings_ShouldReturnFalse_IfKeyNotFound()
        {
            var messagingSettings = new MessagingSettings
            {
                MessageBrokers = new Dictionary<string, IConfigurationSection>()
            };

            var builder = new FeatureBuilder
            {
                MessagingSettings = messagingSettings
            };

            var result = builder.TryGetAdapterSettings<object>("missing", out var output);

            Assert.False(result);
            Assert.Null(output);
        }

        [Fact]
        public void TryGetAdapterSettings_ShouldReturnTrue_IfKeyExists()
        {
            var mockSection = Substitute.For<IConfigurationSection>();
            mockSection.Value.Returns("value");

            var messagingSettings = new MessagingSettings
            {
                MessageBrokers = new Dictionary<string, IConfigurationSection>
                {
                    ["existing"] = mockSection
                }
            };

            var builder = new FeatureBuilder
            {
                MessagingSettings = messagingSettings
            };

            var result = builder.TryGetAdapterSettings("existing", out string output);

            Assert.True(result);
            Assert.Equal("value", output);
        }
    }
}

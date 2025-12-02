using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Pigeon.Messaging.Azure.EventHub;
using Pigeon.Messaging.Azure.EventHub.Consuming;
using Pigeon.Messaging.Azure.EventHub.Producing;
using Pigeon.Messaging.Consuming.Management;
using Pigeon.Messaging.Producing.Management;

namespace Pigeon.Messaging.Azure.EventHub.Tests.DependencyInjection
{
    public class ServicesExtensionsTests
    {
        [Fact]
        public void UseAzureEventHub_Should_Register_Default_Adapters_And_Provider()
        {
            // Arrange
            var services = new ServiceCollection();
            var mockConfiguration = Substitute.For<Microsoft.Extensions.Configuration.IConfiguration>();
            var mockConsumingConfigurator = Substitute.For<Pigeon.Messaging.Consuming.Configuration.IConsumingConfigurator>();
            var messagingSettings = new MessagingSettings { MessageBrokers = new() };
            
            var globalSettingsBuilder = new GlobalSettingsBuilder(services, mockConfiguration, mockConsumingConfigurator, messagingSettings);

            // Act
            globalSettingsBuilder.UseAzureEventHub();

            // Assert
            Assert.Contains(services, d => d.ServiceType == typeof(IEventHubProvider) && d.ImplementationType == typeof(EventHubProvider));
            Assert.Contains(services, d => d.ServiceType == typeof(IMessageBrokerConsumingAdapter) && d.ImplementationType == typeof(EventHubConsumingAdapter));
            Assert.Contains(services, d => d.ServiceType == typeof(IMessageBrokerProducingAdapter) && d.ImplementationType == typeof(EventHubProducingAdapter));
        }

        [Fact]
        public void UseAzureEventHub_Should_Register_IOptions_EventHubSettings_With_Config()
        {
            // Arrange
            var services = new ServiceCollection();
            var mockConfiguration = Substitute.For<Microsoft.Extensions.Configuration.IConfiguration>();
            var mockConsumingConfigurator = Substitute.For<Pigeon.Messaging.Consuming.Configuration.IConsumingConfigurator>();
            var messagingSettings = new MessagingSettings { MessageBrokers = new() };
            
            var globalSettingsBuilder = new GlobalSettingsBuilder(services, mockConfiguration, mockConsumingConfigurator, messagingSettings);
            var customConnectionString = "Endpoint=sb://custom.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=custom-key";

            // Act
            globalSettingsBuilder.UseAzureEventHub(options => options.ConnectionString = customConnectionString);

            // Assert: should have IOptions<AzureEventHubSettings>
            var serviceProvider = services.BuildServiceProvider();
            var options = serviceProvider.GetService<IOptions<AzureEventHubSettings>>();

            Assert.NotNull(options);
            Assert.Equal(customConnectionString, options.Value.ConnectionString);
        }

        [Fact]
        public void UseAzureEventHub_Should_Return_Same_GlobalSettingsBuilder()
        {
            // Arrange
            var services = new ServiceCollection();
            var mockConfiguration = Substitute.For<Microsoft.Extensions.Configuration.IConfiguration>();
            var mockConsumingConfigurator = Substitute.For<Pigeon.Messaging.Consuming.Configuration.IConsumingConfigurator>();
            var messagingSettings = new MessagingSettings { MessageBrokers = new() };
            
            var globalSettingsBuilder = new GlobalSettingsBuilder(services, mockConfiguration, mockConsumingConfigurator, messagingSettings);

            // Act
            var result = globalSettingsBuilder.UseAzureEventHub(options => options.ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=test-key");

            // Assert
            Assert.Same(globalSettingsBuilder, result);
        }
    }
}
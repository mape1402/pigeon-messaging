using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Pigeon.Messaging.Azure.EventGrid;
using Pigeon.Messaging.Azure.EventGrid.Consuming;
using Pigeon.Messaging.Azure.EventGrid.Producing;
using Pigeon.Messaging.Consuming.Management;
using Pigeon.Messaging.Producing.Management;

namespace Pigeon.Messaging.Azure.EventGrid.Tests.DependencyInjection
{
    public class ServicesExtensionsTests
    {
        [Fact]
        public void UseAzureEventGrid_Should_Register_Default_Adapters_And_Provider()
        {
            // Arrange
            var services = new ServiceCollection();
            var mockConfiguration = Substitute.For<Microsoft.Extensions.Configuration.IConfiguration>();
            var mockConsumingConfigurator = Substitute.For<Pigeon.Messaging.Consuming.Configuration.IConsumingConfigurator>();
            var messagingSettings = new MessagingSettings { MessageBrokers = new() };
            
            var globalSettingsBuilder = new GlobalSettingsBuilder(services, mockConfiguration, mockConsumingConfigurator, messagingSettings);

            // Act
            globalSettingsBuilder.UseAzureEventGrid();

            // Assert
            Assert.Contains(services, d => d.ServiceType == typeof(IEventGridProvider) && d.ImplementationType == typeof(EventGridProvider));
            Assert.Contains(services, d => d.ServiceType == typeof(IMessageBrokerConsumingAdapter) && d.ImplementationType == typeof(EventGridConsumingAdapter));
            Assert.Contains(services, d => d.ServiceType == typeof(IMessageBrokerProducingAdapter) && d.ImplementationType == typeof(EventGridProducingAdapter));
        }

        [Fact]
        public void UseAzureEventGrid_Should_Register_IOptions_EventGridSettings_With_Config()
        {
            // Arrange
            var services = new ServiceCollection();
            var mockConfiguration = Substitute.For<Microsoft.Extensions.Configuration.IConfiguration>();
            var mockConsumingConfigurator = Substitute.For<Pigeon.Messaging.Consuming.Configuration.IConsumingConfigurator>();
            var messagingSettings = new MessagingSettings { MessageBrokers = new() };
            
            var globalSettingsBuilder = new GlobalSettingsBuilder(services, mockConfiguration, mockConsumingConfigurator, messagingSettings);
            var customEndpoint = "https://custom.eventgrid.azure.net/api/events";

            // Act
            globalSettingsBuilder.UseAzureEventGrid(options => options.TopicEndpoint = customEndpoint);

            // Assert: should have IOptions<AzureEventGridSettings>
            var serviceProvider = services.BuildServiceProvider();
            var options = serviceProvider.GetService<IOptions<AzureEventGridSettings>>();

            Assert.NotNull(options);
            Assert.Equal(customEndpoint, options.Value.TopicEndpoint);
        }

        [Fact]
        public void UseAzureEventGrid_Should_Return_Same_GlobalSettingsBuilder()
        {
            // Arrange
            var services = new ServiceCollection();
            var mockConfiguration = Substitute.For<Microsoft.Extensions.Configuration.IConfiguration>();
            var mockConsumingConfigurator = Substitute.For<Pigeon.Messaging.Consuming.Configuration.IConsumingConfigurator>();
            var messagingSettings = new MessagingSettings { MessageBrokers = new() };
            
            var globalSettingsBuilder = new GlobalSettingsBuilder(services, mockConfiguration, mockConsumingConfigurator, messagingSettings);

            // Act
            var result = globalSettingsBuilder.UseAzureEventGrid(options => options.TopicEndpoint = "https://test.eventgrid.azure.net/api/events");

            // Assert
            Assert.Same(globalSettingsBuilder, result);
        }
    }
}
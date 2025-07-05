namespace Pigeon.Messaging.RabbitMq.Tests.DependencyInjection
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Options;
    using NSubstitute;
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Consuming.Management;
    using Pigeon.Messaging.Producing.Management;
    using Pigeon.Messaging.Rabbit;
    using Pigeon.Messaging.Rabbit.Consuming;
    using Pigeon.Messaging.Rabbit.Producing;
    using Xunit;

    public class ServicesExtensionsTests
    {
        [Fact]
        public void UseRabbitMq_Should_Register_Default_Adapters_And_ConnectionProvider()
        {
            // Arrange
            var services = new ServiceCollection();
            var globalSettingsBuilder = TestGlobalSettingsBuilder.Create(services);

            // Act
            globalSettingsBuilder.UseRabbitMq();

            // Assert
            Assert.Contains(services, d => d.ServiceType == typeof(IConnectionProvider) && d.ImplementationType == typeof(ConnectionProvider));
            Assert.Contains(services, d => d.ServiceType == typeof(IMessageBrokerConsumingAdapter) && d.ImplementationType == typeof(RabbitConsumingAdapter));
            Assert.Contains(services, d => d.ServiceType == typeof(IMessageBrokerProducingAdapter) && d.ImplementationType == typeof(RabbitProducingAdapter));
        }

        [Fact]
        public void UseRabbitMq_Should_Register_IOptions_RabbitSettings_With_Config()
        {
            // Arrange
            var services = new ServiceCollection();
            var globalSettingsBuilder = TestGlobalSettingsBuilder.Create(services);

            var customHost = "amqp://custom";

            // Act
            globalSettingsBuilder.UseRabbitMq(options => options.Url = customHost);

            // Assert: should have IOptions<RabbitSettings>
            var serviceProvider = services.BuildServiceProvider();
            var options = serviceProvider.GetService<IOptions<RabbitSettings>>();

            Assert.NotNull(options);
            Assert.Equal(customHost, options.Value.Url);
        }

        [Fact]
        public void UseRabbitMq_Should_Return_Same_GlobalSettingsBuilder()
        {
            // Arrange
            var services = new ServiceCollection();
            var globalSettingsBuilder = TestGlobalSettingsBuilder.Create(services);

            // Act
            var result = globalSettingsBuilder.UseRabbitMq(options => options.Url = "amqp://somehost");

            // Assert
            Assert.Same(globalSettingsBuilder, result);
        }

        private class TestGlobalSettingsBuilder : GlobalSettingsBuilder
        {
            private TestGlobalSettingsBuilder(IServiceCollection services)
                : base(services, Substitute.For<IConfiguration>(), Substitute.For<IConsumingConfigurator>(), new MessagingSettings { MessageBrokers = new() })
            {
            }

            public static GlobalSettingsBuilder Create(IServiceCollection services)
            {
                return new TestGlobalSettingsBuilder(services);
            }
        }
    }

}

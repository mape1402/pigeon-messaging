namespace Pigeon.Messaging.InMemory.Tests.DependencyInjection
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using NSubstitute;
    using Pigeon.Messaging.Consuming.Management;
    using Pigeon.Messaging.InMemory;
    using Pigeon.Messaging.Producing.Management;
    using Pigeon.Messaging.Topology;

    public class ServicesExtensionsTests
    {
        [Fact]
        public void UseInMemoryBroker_Should_Register_Broker_And_Adapters()
        {
            var services = new ServiceCollection();
            var builder = TestGlobalSettingsBuilder.Create(services);

            var result = builder.UseInMemoryBroker();

            Assert.Same(builder, result);
            Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IInMemoryBroker));
            Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IMessageBrokerConsumingAdapter));
            Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IMessageBrokerProducingAdapter));
            Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IMessageBrokerTopologyAdapter));
        }

        private sealed class TestGlobalSettingsBuilder : GlobalSettingsBuilder
        {
            private TestGlobalSettingsBuilder(IServiceCollection services)
                : base(
                    services,
                    new ConfigurationBuilder().Build(),
                    Substitute.For<Pigeon.Messaging.Consuming.Configuration.IConsumingConfigurator>(),
                    new MessagingSettings { MessageBrokers = new() })
            {
            }

            public static GlobalSettingsBuilder Create(IServiceCollection services)
                => new TestGlobalSettingsBuilder(services);
        }
    }
}

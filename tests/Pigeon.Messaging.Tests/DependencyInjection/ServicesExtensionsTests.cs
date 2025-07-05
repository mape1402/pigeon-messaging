namespace Pigeon.Messaging.Tests.DependencyInjection
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Consuming.Dispatching;
    using Pigeon.Messaging.Consuming.Management;
    using Pigeon.Messaging.Producing;
    using Pigeon.Messaging.Producing.Management;
    using Xunit;

    public class ServicesExtensionsTests
    {
        [Fact]
        public void AddPigeon_Should_Register_Core_Services()
        {
            var services = new ServiceCollection();
            var configuration = StubConfiguration();

            var builder = services.AddPigeon(configuration, settingsBuilder => { });

            Assert.NotNull(builder);
            Assert.Contains(services, d => d.ServiceType == typeof(IConsumingConfigurator));
            Assert.Contains(services, d => d.ServiceType == typeof(IConsumingDispatcher));
            Assert.Contains(services, d => d.ServiceType == typeof(IConsumingManager));
            Assert.Contains(services, d => d.ServiceType == typeof(IProducer));
            Assert.Contains(services, d => d.ServiceType == typeof(IProducingManager));
        }

        [Fact]
        public void AddConsumeInterceptor_Should_Add_ConsumeInterceptor()
        {
            var services = new ServiceCollection();
            var configuration = StubConfiguration();

            var builder = services.AddPigeon(configuration, settingsBuilder => { });

            builder.AddConsumeInterceptor<SampleInterceptor>();

            // Verifies descriptor is added
            var found = services.Any(d => d.ServiceType == typeof(IConsumeInterceptor) && d.ImplementationType == typeof(SampleInterceptor));
            Assert.True(found);
        }

        [Fact]
        public void AddPublishInterceptor_Should_Add_PublishInterceptor()
        {
            var services = new ServiceCollection();
            var configuration = StubConfiguration();

            var builder = services.AddPigeon(configuration, settingsBuilder => { });

            builder.AddPublishInterceptor<SamplePubInterceptor>();

            var found = services.Any(d => d.ServiceType == typeof(IPublishInterceptor) && d.ImplementationType == typeof(SamplePubInterceptor));
            Assert.True(found);
        }

        private IConfiguration StubConfiguration()
        {
            var json = 
            @"
              {
                ""Pigeon"": {
                    ""Domain"": ""Tests"",
                    ""MessageBrokers"": {
                        ""TestingBroker"": {
                            ""Url"": ""testconnection""
                        }
                    }
                }
              }";

            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(json);
            writer.Flush();
            stream.Position = 0;

            return new ConfigurationBuilder()
                        .AddJsonStream(stream)
                        .Build();
        }

        class SampleInterceptor : IConsumeInterceptor
        {
            public ValueTask Intercept(ConsumeContext context, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        }

        class SamplePubInterceptor : IPublishInterceptor
        {
            public ValueTask Intercept(PublishContext context, CancellationToken cancellationToken = default) 
                => ValueTask.CompletedTask;
        }
    }

}

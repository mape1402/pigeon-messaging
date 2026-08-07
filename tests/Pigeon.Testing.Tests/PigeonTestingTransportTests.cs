namespace Pigeon.Testing.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using Pigeon.Messaging.Producing;

    public class PigeonTestingTransportTests
    {
        [Fact]
        public async Task PublishAsync_Should_Capture_Pending_Message_And_Dispatch_To_Registered_Consumer()
        {
            var services = CreateServices();
            services.AddPigeonTestingConsumers(typeof(CustomerCreatedConsumer).Assembly);

            await using var provider = services.BuildServiceProvider();
            var pigeon = provider.GetRequiredService<IPigeonTestingTransport>();
            var probe = provider.GetRequiredService<CustomerProbe>();
            var customerId = Guid.NewGuid();

            await pigeon.PublishAsync(new CustomerCreatedMessage(customerId));

            Assert.Empty(probe.CustomerIds);
            pigeon.ShouldContainMessage<CustomerCreatedMessage>(message => message.CustomerId == customerId);

            await pigeon.DispatchPendingAsync();

            Assert.Contains(customerId, probe.CustomerIds);
            Assert.True(probe.ConsumeContextWasAvailable);
            pigeon.ShouldContainConsumedMessage<CustomerCreatedMessage>(message => message.CustomerId == customerId);
        }

        [Fact]
        public async Task PublishAsync_Should_Capture_Metadata_From_Publish_Interceptors()
        {
            var services = CreateServices();
            var correlationId = Guid.NewGuid().ToString("N");

            services.AddSingleton(new CorrelationIdHolder { CorrelationId = correlationId });
            services.AddScoped<IPublishInterceptor, CorrelationPublishInterceptor>();
            services.AddPigeonTestingConsumers(typeof(CustomerCreatedConsumer).Assembly);

            await using var provider = services.BuildServiceProvider();
            var pigeon = provider.GetRequiredService<IPigeonTestingTransport>();

            await pigeon.PublishAsync(new CustomerCreatedMessage(Guid.NewGuid()));

            var message = pigeon.ShouldContainMessage<CustomerCreatedMessage>();
            message.Headers["correlation-id"].ShouldBe(correlationId);
            message.CorrelationId.ShouldBe(correlationId);
        }

        [Fact]
        public async Task DispatchPendingAsync_Should_Record_Infrastructure_Failures()
        {
            var services = CreateServices();
            services.AddPigeonTestingConsumers(typeof(CustomerCreatedConsumer).Assembly);

            await using var provider = services.BuildServiceProvider();
            var pigeon = provider.GetRequiredService<IPigeonTestingTransport>();
            var probe = provider.GetRequiredService<CustomerProbe>();

            pigeon.FailNext<CustomerFailedMessage>(new TimeoutException("broker timeout"));

            await pigeon.PublishAsync(new CustomerFailedMessage(Guid.NewGuid()));
            await pigeon.DispatchPendingAsync();

            Assert.Empty(probe.CustomerIds);
            Assert.Single(pigeon.ConsumerFailures);

            var deadLetter = pigeon.ShouldContainDeadLetterMessage<CustomerFailedMessage>();
            Assert.IsType<TimeoutException>(deadLetter.Exception);
            deadLetter.RetryAttempts.ShouldBe(1);
        }

        [Fact]
        public async Task DispatchPendingAsync_Should_Record_Consumer_Failures()
        {
            var services = CreateServices();
            services.AddPigeonTestingConsumers(typeof(CustomerThrowingConsumer).Assembly);

            await using var provider = services.BuildServiceProvider();
            var pigeon = provider.GetRequiredService<IPigeonTestingTransport>();

            await pigeon.PublishAsync(new CustomerThrowingMessage(Guid.NewGuid()));
            await pigeon.DispatchPendingAsync();

            Assert.Single(pigeon.ConsumerFailures);

            var deadLetter = pigeon.ShouldContainDeadLetterMessage<CustomerThrowingMessage>();
            Assert.IsType<InvalidOperationException>(deadLetter.Exception);
            deadLetter.RetryAttempts.ShouldBe(1);
        }

        [Fact]
        public async Task Producer_Should_Publish_Into_Testing_Transport()
        {
            var services = CreateServices();
            services.AddPigeonTestingConsumers(typeof(CustomerCreatedConsumer).Assembly);

            await using var provider = services.BuildServiceProvider();
            var producer = provider.CreateScope().ServiceProvider.GetRequiredService<IProducer>();
            var pigeon = provider.GetRequiredService<IPigeonTestingTransport>();
            var customerId = Guid.NewGuid();

            await producer.PublishAsync(new CustomerCreatedMessage(customerId), nameof(CustomerCreatedMessage));
            await pigeon.DispatchPendingAsync();

            pigeon.ShouldContainMessage<CustomerCreatedMessage>(message => message.CustomerId == customerId);
            pigeon.ShouldContainConsumedMessage<CustomerCreatedMessage>(message => message.CustomerId == customerId);
        }

        private static IServiceCollection CreateServices()
            => new ServiceCollection()
                .AddSingleton<CustomerProbe>();
    }
}

namespace Pigeon.Messaging.Consuming.Dispatching
{
    using Microsoft.Extensions.DependencyInjection;
    using Pigeon.Messaging.Consuming.Configuration;

    internal class ConsumingDispatcher : IConsumingDispatcher
    {
        private readonly IServiceProvider _serviceProvider;

        public ConsumingDispatcher(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public async Task DispatchAsync(string topic, RawPayload rawPayload, CancellationToken cancellationToken = default)
        {
            if(string.IsNullOrWhiteSpace(topic))
                throw new ArgumentNullException(nameof(topic));

            using (var scope = _serviceProvider.CreateScope())
            {
                var consumingConfigurator = scope.ServiceProvider.GetRequiredService<IConsumingConfigurator>();
                var configuration = consumingConfigurator.GetConfiguration(topic, rawPayload.MessageVersion);

                var interceptors = scope.ServiceProvider.GetServices<IConsumeInterceptor>();
                var serializer = scope.ServiceProvider.GetRequiredService<ISerializer>();

                var context = new ConsumeContext
                {
                    CancellationToken = cancellationToken,
                    CreatedOnUtc = rawPayload.CreatedOnUtc,
                    From = rawPayload.Domain,
                    MessageType = configuration.MessageType,
                    MessageVersion = configuration.Version,
                    Services = scope.ServiceProvider,
                    Topic = topic,
                    Message = rawPayload.GetMessage(configuration.MessageType, serializer),
                    RawMetadata = rawPayload.GetMetadata()
                };

                foreach (var interceptor in interceptors)
                    await interceptor.Intercept(context);

                await configuration.Handler(context);
            }
        }
    }
}

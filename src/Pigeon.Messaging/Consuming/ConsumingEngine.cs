namespace Pigeon.Messaging.Consuming
{
    using Microsoft.Extensions.DependencyInjection;
    using Pigeon.Messaging.Consuming.Configuration;

    internal class ConsumingEngine : IConsumingEngine
    {
        private readonly IServiceProvider _serviceProvider;

        public ConsumingEngine(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public async Task HandleAsync(string topic, RawPayload rawPayload, CancellationToken cancellationToken = default)
        {
            if(string.IsNullOrWhiteSpace(topic))
                throw new ArgumentNullException(nameof(topic));

            using (var scope = _serviceProvider.CreateScope())
            {
                var consumingConfigurator = scope.ServiceProvider.GetRequiredService<IConsumingConfigurator>();
                var configuration = consumingConfigurator.GetConfiguration(topic, rawPayload.MessageVersion);

                var interceptors = scope.ServiceProvider.GetServices<IConsumeInterceptor>();

                var rawMetadata = rawPayload.GetMetadata();
                var context = new ConsumeContext
                {
                    CancellationToken = cancellationToken,
                    CreatedOnUtc = rawPayload.CreatedOnUtc,
                    From = rawPayload.Domain,
                    MessageType = configuration.MessageType,
                    MessageVersion = configuration.Version,
                    Services = scope.ServiceProvider,
                    Topic = topic,
                    Message = rawPayload.GetMessage(configuration.MessageType),
                    RawMetadata = rawMetadata.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value)
                };

                foreach (var interceptor in interceptors)
                    await interceptor.Intercept(context);

                await configuration.Handler(context);
            }
        }
    }
}

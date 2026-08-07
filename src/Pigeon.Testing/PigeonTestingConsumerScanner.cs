namespace Pigeon.Testing
{
    using Microsoft.Extensions.DependencyInjection;
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Consuming.Dispatching;
    using Pigeon.Messaging.Contracts;
    using System.Reflection;

    internal sealed class PigeonTestingConsumerScanner
    {
        private static readonly PropertyInfo HubConsumerContextProperty =
            typeof(HubConsumer).GetProperty(nameof(HubConsumer.Context), BindingFlags.Instance | BindingFlags.Public);

        private readonly IServiceCollection _services;
        private readonly IConsumingConfigurator _consumingConfigurator;

        public PigeonTestingConsumerScanner(IServiceCollection services, IConsumingConfigurator consumingConfigurator)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _consumingConfigurator = consumingConfigurator ?? throw new ArgumentNullException(nameof(consumingConfigurator));
        }

        public void Scan(params Assembly[] assemblies)
        {
            if (assemblies == null || assemblies.Length == 0)
                return;

            foreach (var assembly in assemblies.Where(assembly => assembly != null))
            {
                foreach (var consumerType in assembly.GetTypes().Where(IsConcreteHubConsumer))
                    RegisterConsumerType(consumerType);
            }
        }

        private void RegisterConsumerType(Type consumerType)
        {
            _services.AddScoped(consumerType);

            var methods = consumerType
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(method => method.GetCustomAttributes<ConsumerAttribute>().Any());

            foreach (var method in methods)
                RegisterConsumerMethod(consumerType, method);
        }

        private void RegisterConsumerMethod(Type consumerType, MethodInfo method)
        {
            if (method.ReturnType != typeof(Task))
                throw new InvalidOperationException($"Consumer method '{consumerType.Name}.{method.Name}' must return Task.");

            var messageParameter = method
                .GetParameters()
                .FirstOrDefault(parameter => parameter.ParameterType != typeof(CancellationToken));

            if (messageParameter == null)
                throw new InvalidOperationException($"Consumer method '{consumerType.Name}.{method.Name}' must contain one message parameter.");

            var registerMethod = typeof(PigeonTestingConsumerScanner)
                .GetMethod(nameof(RegisterTypedConsumerMethod), BindingFlags.Instance | BindingFlags.NonPublic)
                .MakeGenericMethod(messageParameter.ParameterType);

            registerMethod.Invoke(this, new object[] { consumerType, method });
        }

        private void RegisterTypedConsumerMethod<TMessage>(Type consumerType, MethodInfo method) where TMessage : class
        {
            foreach (var attribute in method.GetCustomAttributes<ConsumerAttribute>())
            {
                var version = SemanticVersion.Parse(attribute.Version);
                var subscription = string.IsNullOrWhiteSpace(attribute.Subscription)
                    ? ConsumerEndpoint.DefaultSubscription
                    : attribute.Subscription;

                _consumingConfigurator.AddConsumer<TMessage>(
                    attribute.Topic,
                    version,
                    subscription,
                    async (context, message) =>
                    {
                        var consumer = (HubConsumer)context.Services.GetRequiredService(consumerType);
                        HubConsumerContextProperty.SetValue(consumer, context);

                        var arguments = method
                            .GetParameters()
                            .Select(parameter => parameter.ParameterType == typeof(CancellationToken)
                                ? (object)context.CancellationToken
                                : message)
                            .ToArray();

                        try
                        {
                            await (Task)method.Invoke(consumer, arguments);
                        }
                        catch (TargetInvocationException exception) when (exception.InnerException != null)
                        {
                            throw exception.InnerException;
                        }
                    });
            }
        }

        private static bool IsConcreteHubConsumer(Type type)
            => typeof(HubConsumer).IsAssignableFrom(type) && type is { IsAbstract: false, IsClass: true };
    }
}

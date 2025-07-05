namespace Pigeon.Messaging.Consuming.Configuration
{
    using Microsoft.Extensions.DependencyInjection;
    using Pigeon.Messaging.Consuming.Dispatching;
    using Pigeon.Messaging.Contracts;
    using System.Reflection;

    /// <summary>
    /// Scans assemblies to discover all non-abstract classes inheriting from <see cref="HubConsumer"/>,
    /// inspects their methods for <see cref="ConsumerAttribute"/>,
    /// and registers the found consumers and their handlers into the DI container and consuming configurator.
    /// </summary>
    internal class ConsumerScanner
    {
        private readonly IServiceCollection _services;
        private readonly IConsumingConfigurator _configurator;

        /// <summary>
        /// Creates a new instance of <see cref="ConsumerScanner"/>.
        /// </summary>
        /// <param name="services">The DI service collection where consumers will be registered.</param>
        /// <param name="configurator">The consuming configurator to register consumer handlers.</param>
        /// <exception cref="ArgumentNullException">If any argument is null.</exception>
        public ConsumerScanner(IServiceCollection services, IConsumingConfigurator configurator)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _configurator = configurator ?? throw new ArgumentNullException(nameof(configurator));
        }

        /// <summary>
        /// Scans the provided assemblies for all concrete types inheriting <see cref="HubConsumer"/>,
        /// then processes their methods decorated with <see cref="ConsumerAttribute"/>.
        /// </summary>
        /// <param name="assemblies">Assemblies to scan.</param>
        public void Scan(params Assembly[] assemblies)
        {
            foreach (var assembly in assemblies)
            {
                var types = assembly.GetTypes()
                                .Where(type =>
                                    type.IsClass &&
                                    !type.IsAbstract &&
                                    typeof(HubConsumer).IsAssignableFrom(type));

                ScanHubConsumers(types);
            }
        }

        /// <summary>
        /// Processes each discovered consumer type by registering
        /// the methods marked with <see cref="ConsumerAttribute"/> as message handlers.
        /// </summary>
        /// <param name="hubConsumers">Enumerable of consumer types.</param>
        internal void ScanHubConsumers(IEnumerable<Type> hubConsumers)
        {
            foreach (var consumerType in hubConsumers)
            {
                // Defensive check, although Scan already filters these.
                if (consumerType.IsAbstract || !typeof(HubConsumer).IsAssignableFrom(consumerType))
                    continue;

                // Get public instance methods declared only on this type that have ConsumerAttribute
                var methods = consumerType
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                    .Where(m => m.GetCustomAttributes(typeof(ConsumerAttribute), false).Any());

                foreach (var method in methods)
                {
                    var consumerAttributes = method.GetCustomAttributes<ConsumerAttribute>(false);

                    foreach (var attr in consumerAttributes)
                    {
                        // Find the message parameter (exclude CancellationToken)
                        var messageParam = method.GetParameters().FirstOrDefault(p => p.ParameterType != typeof(CancellationToken));

                        if (messageParam == null || method.ReturnType != typeof(Task))
                            throw new InvalidOperationException($"Method '{method.Name}' in type '{method.DeclaringType}' has an invalid signature.");

                        var messageType = messageParam.ParameterType;

                        // Parse the semantic version from attribute
                        var version = SemanticVersion.Parse(attr.Version);

                        // Register the consumer handler and DI service
                        RegisterConsumer(consumerType, method, messageType, attr.Topic, version);
                    }
                }
            }
        }

        /// <summary>
        /// Registers a consumer handler in the consuming configurator and
        /// adds the consumer type as scoped service in the DI container.
        /// </summary>
        /// <param name="consumerType">Type of the consumer class.</param>
        /// <param name="method">Method info representing the handler method.</param>
        /// <param name="messageType">Type of the message parameter.</param>
        /// <param name="topic">Topic name to subscribe.</param>
        /// <param name="version">Message semantic version.</param>
        private void RegisterConsumer(Type consumerType, MethodInfo method, Type messageType, string topic, SemanticVersion version)
        {
            // Create generic handler delegate type ConsumeHandler<TMessage>
            var handlerType = typeof(ConsumeHandler<>).MakeGenericType(messageType);

            // Build delegate instance pointing to method
            var handlerDelegate = BuildHandlerDelegate(consumerType, method, messageType);

            // Get the generic AddConsumer<TMessage> method on IConsumingConfigurator
            var addConsumerMethod = typeof(IConsumingConfigurator)
                .GetMethods()
                .First(m => m.Name == "AddConsumer" && m.GetParameters().Length == 3)
                .MakeGenericMethod(messageType);

            // Invoke AddConsumer(topic, version, handlerDelegate)
            addConsumerMethod.Invoke(_configurator, new object[] { topic, version, handlerDelegate });

            // Register consumer class in DI container as scoped
            _services.AddScoped(consumerType);
        }

        /// <summary>
        /// Builds a strongly-typed <see cref="ConsumeHandler{T}"/> delegate
        /// that wraps the consumer method invocation and resolves the consumer instance from DI.
        /// </summary>
        /// <param name="consumerType">Type of the consumer.</param>
        /// <param name="method">Method info of the consumer handler.</param>
        /// <param name="messageType">Type of the message parameter.</param>
        /// <returns>A delegate of type <see cref="ConsumeHandler{T}"/> for the given message type.</returns>
        private object BuildHandlerDelegate(Type consumerType, MethodInfo method, Type messageType)
        {
            // Instantiate the generic HandlerBuilder<T>
            var handler = Activator.CreateInstance(
                typeof(HandlerBuilder<>).MakeGenericType(messageType),
                consumerType, method
            );

            // Invoke the Build() method to get the delegate
            var buildMethod = handler.GetType().GetMethod("Build");

            return buildMethod.Invoke(handler, null);
        }

        /// <summary>
        /// Helper class that constructs a <see cref="ConsumeHandler{T}"/> delegate
        /// to invoke a specific method on a consumer instance resolved from DI.
        /// </summary>
        /// <typeparam name="T">Type of the message.</typeparam>
        private class HandlerBuilder<T> where T : class
        {
            private readonly Type _consumerType;
            private readonly MethodInfo _method;

            /// <summary>
            /// Initializes a new instance of <see cref="HandlerBuilder{T}"/>.
            /// </summary>
            /// <param name="consumerType">The consumer class type.</param>
            /// <param name="method">The consumer method info.</param>
            public HandlerBuilder(Type consumerType, MethodInfo method)
            {
                _consumerType = consumerType;
                _method = method;
            }

            /// <summary>
            /// Builds a delegate that resolves the consumer instance from the DI container,
            /// sets the current context, prepares the method parameters, and invokes the method.
            /// </summary>
            /// <returns>A <see cref="ConsumeHandler{T}"/> delegate for the consumer method.</returns>
            public ConsumeHandler<T> Build()
            {
                return (ConsumeContext ctx, T msg) =>
                {
                    var serviceProvider = ctx.Services;
                    var instance = (HubConsumer)serviceProvider.GetRequiredService(_consumerType);

                    // Set the current consume context on the consumer instance
                    instance.Context = ctx;

                    var parameters = _method.GetParameters();
                    var args = new object[parameters.Length];
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        if (parameters[i].ParameterType == typeof(CancellationToken))
                            args[i] = ctx.CancellationToken;
                        else
                            args[i] = msg;
                    }

                    // Invoke the consumer method
                    var result = _method.Invoke(instance, args);

                    // Return the Task or completed task if method is synchronous
                    return result is Task task ? task : Task.CompletedTask;
                };
            }
        }
    }
}

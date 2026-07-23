namespace Pigeon.Messaging.Topology
{
    using Microsoft.Extensions.Options;
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Producing;
    using System.Collections.Concurrent;

    internal class TopologyProvisioningService : ITopologyProvisioningService
    {
        private readonly IEnumerable<IMessageBrokerTopologyAdapter> _adapters;
        private readonly IConsumingConfigurator _consumingConfigurator;
        private readonly GlobalSettings _settings;
        private readonly ConcurrentDictionary<string, Lazy<Task>> _ensuredTopology = new();

        public TopologyProvisioningService(
            IEnumerable<IMessageBrokerTopologyAdapter> adapters,
            IConsumingConfigurator consumingConfigurator,
            IOptions<GlobalSettings> settings)
        {
            _adapters = adapters ?? throw new ArgumentNullException(nameof(adapters));
            _consumingConfigurator = consumingConfigurator ?? throw new ArgumentNullException(nameof(consumingConfigurator));
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        }

        public async Task EnsureStartupTopologyAsync(CancellationToken cancellationToken = default)
        {
            if (!_settings.TopologyProvisioningMode.HasFlag(TopologyProvisioningMode.OnStartup))
                return;

            foreach (var endpoint in _consumingConfigurator.GetAllEndpoints())
                await EnsureConsumeTopologyCoreAsync(endpoint, cancellationToken);
        }

        public Task EnsurePublishTopologyAsync(PublishingRoute route, CancellationToken cancellationToken = default)
        {
            if (!_settings.TopologyProvisioningMode.HasFlag(TopologyProvisioningMode.OnPublish))
                return Task.CompletedTask;

            return EnsurePublishTopologyCoreAsync(route, cancellationToken);
        }

        public Task EnsureConsumeTopologyAsync(ConsumerEndpoint endpoint, CancellationToken cancellationToken = default)
        {
            if (!_settings.TopologyProvisioningMode.HasFlag(TopologyProvisioningMode.OnConsume))
                return Task.CompletedTask;

            return EnsureConsumeTopologyCoreAsync(endpoint, cancellationToken);
        }

        private Task EnsurePublishTopologyCoreAsync(PublishingRoute route, CancellationToken cancellationToken)
            => EnsureForEachAdapterAsync(
                adapter => $"publish:{adapter.BrokerName}:{route.Exchange}:{route.RoutingKey}:{route.Topic}",
                adapter => adapter.EnsurePublishTopologyAsync(route, cancellationToken));

        private Task EnsureConsumeTopologyCoreAsync(ConsumerEndpoint endpoint, CancellationToken cancellationToken)
            => EnsureForEachAdapterAsync(
                adapter => $"consume:{adapter.BrokerName}:{endpoint.Topic}:{endpoint.Subscription}",
                adapter => adapter.EnsureConsumeTopologyAsync(endpoint, cancellationToken));

        private async Task EnsureForEachAdapterAsync(Func<IMessageBrokerTopologyAdapter, string> keyFactory, Func<IMessageBrokerTopologyAdapter, Task> ensure)
        {
            foreach (var adapter in _adapters)
                await EnsureOnceAsync(keyFactory(adapter), () => ensure(adapter));
        }

        private async Task EnsureOnceAsync(string key, Func<Task> ensure)
        {
            var lazyTask = _ensuredTopology.GetOrAdd(
                key,
                _ => new Lazy<Task>(ensure, LazyThreadSafetyMode.ExecutionAndPublication));

            try
            {
                await lazyTask.Value;
            }
            catch
            {
                _ensuredTopology.TryRemove(key, out _);
                throw;
            }
        }
    }
}

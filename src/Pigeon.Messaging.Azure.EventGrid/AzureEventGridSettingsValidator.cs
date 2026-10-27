namespace Pigeon.Messaging.Azure.EventGrid
{
    internal static class AzureEventGridSettingsValidator
    {
        public static void Validate(AzureEventGridSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            ValidateServiceBus(settings);

            if (settings.Endpoints == null)
                return;

            foreach (var endpoint in settings.Endpoints)
                ValidateEndpoint(endpoint.Key, endpoint.Value);
        }

        public static bool HasServiceBusTransport(AzureEventGridSettings settings)
            => !string.IsNullOrWhiteSpace(settings.ServiceBusEndPoint)
                || !string.IsNullOrWhiteSpace(settings.ServiceBusFullyQualifiedNamespace)
                || settings.ServiceBusCredential != null;

        public static void ValidateServiceBus(AzureEventGridSettings settings)
        {
            var hasConnectionString = !string.IsNullOrWhiteSpace(settings.ServiceBusEndPoint);
            var hasNamespace = !string.IsNullOrWhiteSpace(settings.ServiceBusFullyQualifiedNamespace);
            var hasCredential = settings.ServiceBusCredential != null;

            if (!hasConnectionString && !hasNamespace && !hasCredential)
                return;

            if (hasConnectionString)
            {
                if (hasNamespace || hasCredential)
                {
                    throw new InvalidOperationException(
                        "Azure Event Grid Service Bus configuration cannot combine ServiceBusEndPoint with ServiceBusFullyQualifiedNamespace or ServiceBusCredential.");
                }

                return;
            }

            if (!hasNamespace)
                throw new InvalidOperationException("Azure Event Grid Service Bus credential authentication requires ServiceBusFullyQualifiedNamespace.");

            if (!hasCredential)
                throw new InvalidOperationException("Azure Event Grid Service Bus credential authentication requires ServiceBusCredential.");
        }

        private static void ValidateEndpoint(string name, Endpoint endpoint)
        {
            if (endpoint == null)
                throw new InvalidOperationException($"Azure Event Grid endpoint '{name}' is not configured.");

            if (string.IsNullOrWhiteSpace(endpoint.Url))
                throw new InvalidOperationException($"Azure Event Grid endpoint '{name}' requires Url.");

            var hasAccessKey = !string.IsNullOrWhiteSpace(endpoint.AccessKey);
            var hasCredential = endpoint.Credential != null;

            if (hasAccessKey && hasCredential)
            {
                throw new InvalidOperationException(
                    $"Azure Event Grid endpoint '{name}' cannot combine AccessKey with Credential.");
            }

            if (!hasAccessKey && !hasCredential)
                throw new InvalidOperationException($"Azure Event Grid endpoint '{name}' requires AccessKey or Credential.");
        }
    }
}

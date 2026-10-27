namespace Pigeon.Messaging.Azure.EventHub
{
    internal static class AzureEventHubSettingsValidator
    {
        public static void Validate(AzureEventHubSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            var hasConnectionString = !string.IsNullOrWhiteSpace(settings.ConnectionString);
            var hasNamespace = !string.IsNullOrWhiteSpace(settings.FullyQualifiedNamespace);
            var hasCredential = settings.Credential != null;

            if (hasConnectionString)
            {
                if (hasNamespace || hasCredential)
                {
                    throw new InvalidOperationException(
                        "Azure Event Hubs configuration cannot combine ConnectionString with FullyQualifiedNamespace or Credential.");
                }

                return;
            }

            if (!hasNamespace && !hasCredential)
            {
                throw new InvalidOperationException(
                    "Azure Event Hubs configuration requires ConnectionString or both FullyQualifiedNamespace and Credential.");
            }

            if (!hasNamespace)
                throw new InvalidOperationException("Azure Event Hubs credential authentication requires FullyQualifiedNamespace.");

            if (!hasCredential)
                throw new InvalidOperationException("Azure Event Hubs credential authentication requires Credential.");
        }
    }
}

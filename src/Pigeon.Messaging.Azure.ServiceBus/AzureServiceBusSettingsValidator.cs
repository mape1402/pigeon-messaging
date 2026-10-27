namespace Pigeon.Messaging.Azure.ServiceBus
{
    internal static class AzureServiceBusSettingsValidator
    {
        public static void Validate(AzureServiceBusSettings settings)
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
                        "Azure Service Bus configuration cannot combine ConnectionString with FullyQualifiedNamespace or Credential.");
                }

                return;
            }

            if (!hasNamespace && !hasCredential)
            {
                throw new InvalidOperationException(
                    "Azure Service Bus configuration requires ConnectionString or both FullyQualifiedNamespace and Credential.");
            }

            if (!hasNamespace)
                throw new InvalidOperationException("Azure Service Bus credential authentication requires FullyQualifiedNamespace.");

            if (!hasCredential)
                throw new InvalidOperationException("Azure Service Bus credential authentication requires Credential.");
        }
    }
}

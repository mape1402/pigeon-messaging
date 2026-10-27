namespace Pigeon.Messaging.Kafka
{
    internal static class KafkaSettingsValidator
    {
        public static void Validate(KafkaSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            if (string.IsNullOrWhiteSpace(settings.BootstrapServers))
                throw new InvalidOperationException("Kafka configuration requires BootstrapServers.");
        }
    }
}

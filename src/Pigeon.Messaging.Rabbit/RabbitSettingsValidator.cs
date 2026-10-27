namespace Pigeon.Messaging.Rabbit
{
    internal static class RabbitSettingsValidator
    {
        public static void Validate(RabbitSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            if (string.IsNullOrWhiteSpace(settings.Url) && settings.ConfigureConnectionFactory == null)
            {
                throw new InvalidOperationException(
                    "RabbitMQ configuration requires Url or ConfigureConnectionFactory.");
            }

            if (!string.IsNullOrWhiteSpace(settings.Url)
                && !Uri.TryCreate(settings.Url, UriKind.Absolute, out _))
            {
                throw new InvalidOperationException("RabbitMQ configuration requires Url to be an absolute URI.");
            }
        }
    }
}

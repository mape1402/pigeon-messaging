namespace Pigeon.Messaging.Kafka
{
    using Confluent.Kafka;

    internal static class KafkaClientBuilderAuthentication
    {
        public static ProducerBuilder<TKey, TValue> Apply<TKey, TValue>(
            ProducerBuilder<TKey, TValue> builder,
            Action<IClient, string> refreshHandler)
        {
            if (refreshHandler != null)
                builder.SetOAuthBearerTokenRefreshHandler((client, configuration) => refreshHandler(client, configuration));

            return builder;
        }

        public static ConsumerBuilder<TKey, TValue> Apply<TKey, TValue>(
            ConsumerBuilder<TKey, TValue> builder,
            Action<IClient, string> refreshHandler)
        {
            if (refreshHandler != null)
                builder.SetOAuthBearerTokenRefreshHandler((client, configuration) => refreshHandler(client, configuration));

            return builder;
        }

        public static AdminClientBuilder Apply(AdminClientBuilder builder, Action<IClient, string> refreshHandler)
        {
            if (refreshHandler != null)
            {
                builder.SetOAuthBearerTokenRefreshHandler(
                    (client, configuration) => refreshHandler(client, configuration));
            }

            return builder;
        }
    }
}

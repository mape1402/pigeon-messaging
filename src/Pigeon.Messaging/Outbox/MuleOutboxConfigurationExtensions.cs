namespace Pigeon.Messaging.Outbox
{
    using Mule;
    using Mule.Configuration;

    /// <summary>
    /// Maps Pigeon outbox settings to Mule durable action settings.
    /// </summary>
    public static class MuleOutboxConfigurationExtensions
    {
        /// <summary>
        /// Registers the Pigeon publish durable action and applies outbox settings to Mule.
        /// </summary>
        /// <param name="builder">The Mule registration builder.</param>
        /// <param name="settings">The Pigeon outbox settings.</param>
        /// <returns>The same Mule registration builder.</returns>
        public static IMuleRegistrationBuilder AddPigeonOutboxAction(
            this IMuleRegistrationBuilder builder,
            OutboxSettings settings)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            settings ??= new OutboxSettings();

            return builder
                .Configure(mule =>
                {
                    mule.ImmediateDispatch = settings.ImmediateDispatch;
                    mule.DispatchInterval = settings.DispatchInterval;
                    mule.DispatchQueueCapacity = settings.DispatchQueueCapacity;
                    mule.DispatchBatchSize = settings.DispatchBatchSize;
                    mule.MaxAttempts = settings.MaxRetries;
                    mule.RetryDelay = settings.RetryDelay;
                    mule.LockTimeout = settings.LockTimeout;
                    mule.CleanupInterval = settings.CleanInterval;
                    mule.CleanupBatchSize = settings.CleanBatchSize;
                    mule.CompletedRetention = settings.PublishedMessageRetention;
                    mule.RecoveryMode = MuleRecoveryMode.Scheduled;
                    mule.CleanupMode = MuleCleanupMode.Scheduled;
                })
                .For<PigeonPublishOutboxAction, OutboxMessage>(PigeonOutboxActionKeys.Publish);
        }
    }
}

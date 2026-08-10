namespace Pigeon.Messaging.Outbox
{
    using Mule;
    using Pigeon.Messaging.Producing.Management;

    /// <summary>
    /// Mule durable action that publishes a prepared Pigeon outbox message to the configured broker adapter.
    /// </summary>
    [MuleAction("pigeon.publish.v1")]
    public sealed class PigeonPublishOutboxAction : IMuleAction<OutboxMessage>
    {
        private readonly IProducingManager _producingManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="PigeonPublishOutboxAction"/> class.
        /// </summary>
        /// <param name="producingManager">The Pigeon producing manager.</param>
        public PigeonPublishOutboxAction(IProducingManager producingManager)
        {
            _producingManager = producingManager ?? throw new ArgumentNullException(nameof(producingManager));
        }

        /// <inheritdoc />
        public async ValueTask ExecuteAsync(MuleActionContext<OutboxMessage> context, CancellationToken cancellationToken)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            await _producingManager.PushOutboxAsync(context.Payload, cancellationToken);
        }
    }
}

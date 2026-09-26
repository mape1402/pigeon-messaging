namespace Pigeon.Messaging
{
    internal sealed class WrappedPayloadJsonPropertyNames
    {
        public WrappedPayloadJsonPropertyNames(
            string domain,
            string messageVersion,
            string createdOnUtc,
            string message,
            string metadata)
        {
            Domain = domain ?? throw new ArgumentNullException(nameof(domain));
            MessageVersion = messageVersion ?? throw new ArgumentNullException(nameof(messageVersion));
            CreatedOnUtc = createdOnUtc ?? throw new ArgumentNullException(nameof(createdOnUtc));
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        }

        public string Domain { get; }

        public string MessageVersion { get; }

        public string CreatedOnUtc { get; }

        public string Message { get; }

        public string Metadata { get; }

        public string[] Required => new[] { Domain, MessageVersion, CreatedOnUtc, Message };
    }
}

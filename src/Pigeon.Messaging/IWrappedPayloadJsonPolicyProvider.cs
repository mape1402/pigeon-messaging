namespace Pigeon.Messaging
{
    using Pigeon.Messaging.Contracts;

    /// <summary>
    /// Provides the JSON naming policy used by Pigeon wrapped payloads.
    /// </summary>
    public interface IWrappedPayloadJsonPolicyProvider
    {
        /// <summary>
        /// Gets the policy name written to and expected from wrapped payloads.
        /// </summary>
        string PolicyName { get; }

        /// <summary>
        /// Gets the serialization info written to wrapped payloads.
        /// </summary>
        WrappedPayloadSerializationInfo SerializationInfo { get; }
    }
}

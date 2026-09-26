namespace Pigeon.Messaging
{
    using Pigeon.Messaging.Contracts;
    using System.Text.Json;

    internal sealed class WrappedPayloadJsonPolicyProvider : IWrappedPayloadJsonPolicyProvider
    {
        private readonly JsonSerializerOptions _serializerOptions;

        public WrappedPayloadJsonPolicyProvider(JsonSerializerOptions serializerOptions)
        {
            _serializerOptions = serializerOptions ?? throw new ArgumentNullException(nameof(serializerOptions));
        }

        public static WrappedPayloadJsonPolicyProvider Default { get; } = new(new JsonSerializerOptions());

        public string PolicyName => Policy.Name;

        public WrappedPayloadSerializationInfo SerializationInfo => Policy.SerializationInfo;

        internal WrappedPayloadJsonPolicy Policy => WrappedPayloadJsonPolicy.Create(_serializerOptions);
    }
}

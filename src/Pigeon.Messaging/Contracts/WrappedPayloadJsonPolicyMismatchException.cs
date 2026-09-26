namespace Pigeon.Messaging.Contracts
{
    using System.Text.Json;

    /// <summary>
    /// Represents a mismatch between the configured Pigeon wrapped payload JSON policy
    /// and the policy declared or detected in an incoming payload.
    /// </summary>
    public sealed class WrappedPayloadJsonPolicyMismatchException : JsonException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WrappedPayloadJsonPolicyMismatchException"/> class.
        /// </summary>
        /// <param name="expectedPolicy">The policy configured by the current consumer.</param>
        /// <param name="actualPolicy">The policy declared or detected in the payload.</param>
        /// <param name="propertyName">The property that exposed the mismatch, when available.</param>
        /// <param name="isLegacyPayload">Whether the payload omitted the fixed Pigeon format marker.</param>
        public WrappedPayloadJsonPolicyMismatchException(
            string expectedPolicy,
            string actualPolicy,
            string propertyName = null,
            bool isLegacyPayload = false)
            : base(CreateMessage(expectedPolicy, actualPolicy, propertyName, isLegacyPayload))
        {
            ExpectedPolicy = expectedPolicy;
            ActualPolicy = actualPolicy;
            PropertyName = propertyName;
            IsLegacyPayload = isLegacyPayload;
        }

        /// <summary>
        /// Gets the policy configured by the current consumer.
        /// </summary>
        public string ExpectedPolicy { get; }

        /// <summary>
        /// Gets the policy declared or detected in the payload.
        /// </summary>
        public string ActualPolicy { get; }

        /// <summary>
        /// Gets the property that exposed the mismatch, when available.
        /// </summary>
        public string PropertyName { get; }

        /// <summary>
        /// Gets a value indicating whether the payload omitted the fixed Pigeon format marker.
        /// </summary>
        public bool IsLegacyPayload { get; }

        private static string CreateMessage(
            string expectedPolicy,
            string actualPolicy,
            string propertyName,
            bool isLegacyPayload)
        {
            var source = isLegacyPayload
                ? "The payload does not include '$pigeon', so Pigeon treated it as a legacy payload"
                : "The payload declares a different '$pigeon' property naming policy";

            var property = string.IsNullOrWhiteSpace(propertyName)
                ? string.Empty
                : $" Property '{propertyName}' exposed the mismatch.";

            return $"WrappedPayload JSON policy mismatch. {source}. Expected policy '{expectedPolicy}', actual policy '{actualPolicy}'.{property}";
        }
    }
}

namespace Pigeon.Messaging.Contracts
{
    /// <summary>
    /// Known JSON naming policy names used in Pigeon wrapped payloads.
    /// </summary>
    public static class WrappedPayloadJsonPolicyNames
    {
        /// <summary>
        /// The default System.Text.Json property naming policy.
        /// </summary>
        public const string Default = "Default";

        /// <summary>
        /// The System.Text.Json camelCase property naming policy.
        /// </summary>
        public const string CamelCase = "CamelCase";
    }
}

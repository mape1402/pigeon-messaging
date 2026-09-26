namespace Pigeon.Messaging.Consuming
{
    using Pigeon.Messaging.Contracts;
    using System.Text.Json;

    internal sealed class RawPayloadFactory : IRawPayloadFactory
    {
        private const string PigeonPropertyName = "$pigeon";
        private const string FormatVersionPropertyName = "formatVersion";
        private const string PropertyNamingPolicyPropertyName = "propertyNamingPolicy";

        private readonly IWrappedPayloadJsonPolicyProvider _policyProvider;

        public RawPayloadFactory(IWrappedPayloadJsonPolicyProvider policyProvider)
        {
            _policyProvider = policyProvider ?? throw new ArgumentNullException(nameof(policyProvider));
        }

        public RawPayload Create(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentNullException(nameof(json));

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var policy = GetPolicy();

            if (root.TryGetProperty(PigeonPropertyName, out var pigeonElement))
            {
                var declaredPolicy = ReadDeclaredPolicy(pigeonElement);
                if (!string.Equals(declaredPolicy, policy.Name, StringComparison.Ordinal))
                {
                    throw new WrappedPayloadJsonPolicyMismatchException(
                        policy.Name,
                        declaredPolicy,
                        PigeonPropertyName);
                }

                return new RawPayload(json, policy.PropertyNames);
            }

            return CreateLegacyPayload(json, root, policy);
        }

        private RawPayload CreateLegacyPayload(string json, JsonElement root, WrappedPayloadJsonPolicy policy)
        {
            if (HasRequiredProperties(root, policy.PropertyNames))
                return new RawPayload(json, policy.PropertyNames);

            var defaultMatches = HasRequiredProperties(root, WrappedPayloadJsonPolicy.Default.PropertyNames);
            var camelMatches = HasRequiredProperties(root, WrappedPayloadJsonPolicy.CamelCase.PropertyNames);

            if (defaultMatches)
            {
                throw new WrappedPayloadJsonPolicyMismatchException(
                    policy.Name,
                    WrappedPayloadJsonPolicyNames.Default,
                    WrappedPayloadJsonPolicy.Default.PropertyNames.Domain,
                    true);
            }

            if (camelMatches)
            {
                throw new WrappedPayloadJsonPolicyMismatchException(
                    policy.Name,
                    WrappedPayloadJsonPolicyNames.CamelCase,
                    WrappedPayloadJsonPolicy.CamelCase.PropertyNames.Domain,
                    true);
            }

            if (HasAnyKnownWrapperProperty(root))
                throw new JsonException("Invalid legacy WrappedPayload JSON. The payload contains a partial or mixed wrapper shape.");

            throw new JsonException("Invalid WrappedPayload JSON. The payload does not contain a recognized Pigeon wrapper shape.");
        }

        private WrappedPayloadJsonPolicy GetPolicy()
            => _policyProvider is WrappedPayloadJsonPolicyProvider provider
                ? provider.Policy
                : WrappedPayloadJsonPolicy.Default;

        private static string ReadDeclaredPolicy(JsonElement pigeonElement)
        {
            if (pigeonElement.ValueKind != JsonValueKind.Object)
                throw new JsonException("Invalid '$pigeon' property. Expected an object.");

            if (pigeonElement.TryGetProperty(FormatVersionPropertyName, out var formatVersionElement) &&
                formatVersionElement.ValueKind == JsonValueKind.String &&
                !string.Equals(formatVersionElement.GetString(), WrappedPayloadSerializationInfo.CurrentFormatVersion, StringComparison.Ordinal))
            {
                throw new JsonException($"Unsupported WrappedPayload format version '{formatVersionElement.GetString()}'.");
            }

            if (!pigeonElement.TryGetProperty(PropertyNamingPolicyPropertyName, out var policyElement) ||
                policyElement.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(policyElement.GetString()))
            {
                throw new JsonException("Invalid '$pigeon.propertyNamingPolicy' property.");
            }

            return policyElement.GetString()!;
        }

        private static bool HasRequiredProperties(JsonElement root, WrappedPayloadJsonPropertyNames propertyNames)
            => propertyNames.Required.All(propertyName => root.TryGetProperty(propertyName, out _));

        private static bool HasAnyKnownWrapperProperty(JsonElement root)
            => HasAnyProperty(root, WrappedPayloadJsonPolicy.Default.PropertyNames)
                || HasAnyProperty(root, WrappedPayloadJsonPolicy.CamelCase.PropertyNames);

        private static bool HasAnyProperty(JsonElement root, WrappedPayloadJsonPropertyNames propertyNames)
            => propertyNames.Required.Any(propertyName => root.TryGetProperty(propertyName, out _));
    }
}

namespace Pigeon.Messaging
{
    using Pigeon.Messaging.Contracts;
    using System.Text.Json;

    internal sealed class WrappedPayloadJsonPolicy
    {
        private WrappedPayloadJsonPolicy(string name, WrappedPayloadJsonPropertyNames propertyNames)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            PropertyNames = propertyNames ?? throw new ArgumentNullException(nameof(propertyNames));
            SerializationInfo = new WrappedPayloadSerializationInfo
            {
                FormatVersion = WrappedPayloadSerializationInfo.CurrentFormatVersion,
                PropertyNamingPolicy = name
            };
        }

        public static WrappedPayloadJsonPolicy Default { get; } = Create((JsonNamingPolicy)null);

        public static WrappedPayloadJsonPolicy CamelCase { get; } = Create(JsonNamingPolicy.CamelCase);

        public string Name { get; }

        public WrappedPayloadJsonPropertyNames PropertyNames { get; }

        public WrappedPayloadSerializationInfo SerializationInfo { get; }

        public static WrappedPayloadJsonPolicy Create(JsonSerializerOptions options)
            => Create(options?.PropertyNamingPolicy);

        private static WrappedPayloadJsonPolicy Create(JsonNamingPolicy namingPolicy)
        {
            var name = ResolveName(namingPolicy);

            return new WrappedPayloadJsonPolicy(
                name,
                new WrappedPayloadJsonPropertyNames(
                    ResolvePropertyName(namingPolicy, nameof(WrappedPayload<object>.Domain)),
                    ResolvePropertyName(namingPolicy, nameof(WrappedPayload<object>.MessageVersion)),
                    ResolvePropertyName(namingPolicy, nameof(WrappedPayload<object>.CreatedOnUtc)),
                    ResolvePropertyName(namingPolicy, nameof(WrappedPayload<object>.Message)),
                    ResolvePropertyName(namingPolicy, nameof(WrappedPayload<object>.Metadata))));
        }

        private static string ResolveName(JsonNamingPolicy namingPolicy)
        {
            if (namingPolicy == null)
                return WrappedPayloadJsonPolicyNames.Default;

            if (ReferenceEquals(namingPolicy, JsonNamingPolicy.CamelCase))
                return WrappedPayloadJsonPolicyNames.CamelCase;

            return namingPolicy.GetType().FullName ?? namingPolicy.GetType().Name;
        }

        private static string ResolvePropertyName(JsonNamingPolicy namingPolicy, string propertyName)
            => namingPolicy?.ConvertName(propertyName) ?? propertyName;
    }
}

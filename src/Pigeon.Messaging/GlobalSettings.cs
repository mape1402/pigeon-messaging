namespace Pigeon.Messaging
{
    using System.Reflection;

    /// <summary>
    /// Represents the global settings for the Pigeon messaging infrastructure.
    /// </summary>
    /// <remarks>
    /// These settings define shared configuration values like the domain name
    /// used for message scoping and the list of assemblies to scan for
    /// consumer endpoints marked with <c>ConsumerAttribute</c>.
    /// </remarks>
    public class GlobalSettings
    {
        /// <summary>
        /// Gets or sets the logical domain that identifies the boundary
        /// for published and consumed messages.
        /// This can be used for multi-tenant isolation or to categorize messages.
        /// </summary>
        public string Domain { get; set; }

        /// <summary>
        /// Gets or sets the assemblies that should be scanned for
        /// classes inheriting from <c>HubConsumer</c> and decorated
        /// with consumer attributes.
        /// </summary>
        /// <remarks>
        /// By default, this is an empty array and must be populated
        /// via the <c>GlobalSettingsBuilder</c> during configuration.
        /// </remarks>
        public Assembly[] TargetAssemblies { get; set; } = Array.Empty<Assembly>();
    }
}

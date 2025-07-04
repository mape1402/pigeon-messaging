namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Represents the builder interface returned by <c>AddPigeon</c>,
    /// allowing further fluent configuration of the messaging framework.
    /// </summary>
    /// <remarks>
    /// This builder exposes access to the <see cref="GlobalSettingsBuilder"/>,
    /// which contains methods for adding features, consumers, interceptors,
    /// or any custom service registrations related to Pigeon.
    /// </remarks>
    public interface IPigeonServiceBuilder
    {
        /// <summary>
        /// Gets the underlying <see cref="GlobalSettingsBuilder"/>
        /// that can be used to customize global messaging settings,
        /// add adapters, or register additional services.
        /// </summary>
        GlobalSettingsBuilder GlobalSettingsBuilder { get; }
    }
}

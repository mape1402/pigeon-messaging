namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    internal class PigeonServiceBuilder : IPigeonServiceBuilder
    {
        public PigeonServiceBuilder(GlobalSettingsBuilder settingsBuilder)
        {
            GlobalSettingsBuilder = settingsBuilder ?? throw new ArgumentNullException(nameof(settingsBuilder));
        }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        public GlobalSettingsBuilder GlobalSettingsBuilder { get; }
    }
}

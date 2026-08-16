namespace Pigeon.Messaging.Consuming.Management
{
    /// <summary>
    /// Exposes consumer execution diagnostics for health checks and operational dashboards.
    /// </summary>
    public interface IConsumerExecutionDiagnostics
    {
        /// <summary>
        /// Gets a point-in-time snapshot of consumer execution state.
        /// </summary>
        /// <returns>The current consumer execution diagnostics snapshot.</returns>
        ConsumerExecutionDiagnosticsSnapshot GetSnapshot();
    }
}

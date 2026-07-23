namespace Pigeon.Messaging.Topology
{
    /// <summary>
    /// Defines when Pigeon should create or ensure broker topology.
    /// </summary>
    [Flags]
    public enum TopologyProvisioningMode
    {
        /// <summary>
        /// Pigeon does not create broker resources.
        /// </summary>
        Manual = 0,

        /// <summary>
        /// Pigeon ensures configured topology during application startup.
        /// </summary>
        OnStartup = 1,

        /// <summary>
        /// Pigeon ensures publish topology before publishing a message.
        /// </summary>
        OnPublish = 2,

        /// <summary>
        /// Pigeon ensures consume topology before starting consumers.
        /// </summary>
        OnConsume = 4
    }
}

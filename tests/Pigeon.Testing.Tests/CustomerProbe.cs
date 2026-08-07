namespace Pigeon.Testing.Tests
{
    public sealed class CustomerProbe
    {
        public List<Guid> CustomerIds { get; } = new();

        public bool ConsumeContextWasAvailable { get; set; }
    }
}

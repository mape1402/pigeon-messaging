namespace Pigeon.Messaging.Rabbit.Sample
{
    internal sealed class OrderCreatedMessage
    {
        public string OrderId { get; set; }

        public DateTimeOffset CreatedOnUtc { get; set; }
    }
}

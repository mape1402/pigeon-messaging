namespace Pigeon.Messaging.Consuming
{
    internal interface IRawPayloadFactory
    {
        RawPayload Create(string json);
    }
}

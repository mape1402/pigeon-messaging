using Pigeon.Messaging.Producing;

internal sealed record PublishedEnvelope(string Kind, string MessageType, string Description, PublishingRoute Route);

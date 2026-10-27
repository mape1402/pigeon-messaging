namespace Pigeon.Messaging.Azure.EventHub.Tests;

using Microsoft.Extensions.Options;

public class AzureEventHubClientFactoryTests
{
    [Fact]
    public async Task CredentialMode_CreatesProducerAndConsumer()
    {
        var factory = CreateFactory();
        await using var producer = factory.CreateProducer("orders");
        await using var consumer = factory.CreateConsumer("billing", "orders");

        Assert.Equal("test.servicebus.windows.net", producer.FullyQualifiedNamespace);
        Assert.Equal("test.servicebus.windows.net", consumer.FullyQualifiedNamespace);
    }

    [Fact]
    public async Task ConnectionStringMode_CreatesProducer()
    {
        var factory = new AzureEventHubClientFactory(Options.Create(new AzureEventHubSettings
        {
            ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=Root;SharedAccessKey=test"
        }));
        await using var producer = factory.CreateProducer("orders");

        Assert.Equal("test.servicebus.windows.net", producer.FullyQualifiedNamespace);
    }

    private static AzureEventHubClientFactory CreateFactory()
        => new(Options.Create(new AzureEventHubSettings
        {
            FullyQualifiedNamespace = "test.servicebus.windows.net",
            Credential = new TestTokenCredential()
        }));
}

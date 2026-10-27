namespace Pigeon.Messaging.Azure.ServiceBus.Tests;

using Microsoft.Extensions.Options;

public class AzureServiceBusClientFactoryTests
{
    [Fact]
    public async Task CredentialMode_CreatesMessagingAndAdministrationClients()
    {
        var factory = CreateFactory();
        await using var client = factory.CreateMessagingClient();
        var administrationClient = factory.CreateAdministrationClient();

        Assert.Equal("test.servicebus.windows.net", client.FullyQualifiedNamespace);
        Assert.NotNull(administrationClient);
    }

    [Fact]
    public async Task ConnectionStringMode_CreatesMessagingClient()
    {
        var factory = new AzureServiceBusClientFactory(Options.Create(new AzureServiceBusSettings
        {
            ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=Root;SharedAccessKey=test"
        }));

        await using var client = factory.CreateMessagingClient();

        Assert.Equal("test.servicebus.windows.net", client.FullyQualifiedNamespace);
    }

    [Fact]
    public void CredentialMode_CreatesTopologyAdapter()
    {
        var adapter = new ServiceBusTopologyAdapter(CreateFactory());

        Assert.Equal("AzureServiceBus", adapter.BrokerName);
    }

    private static AzureServiceBusClientFactory CreateFactory()
        => new(Options.Create(new AzureServiceBusSettings
        {
            FullyQualifiedNamespace = "test.servicebus.windows.net",
            Credential = new TestTokenCredential()
        }));
}

namespace Pigeon.Messaging.Azure.EventGrid.Tests;

using Microsoft.Extensions.Options;

public class AzureEventGridClientFactoryTests
{
    [Fact]
    public async Task CredentialModes_CreatePublisherAndServiceBusClient()
    {
        var settings = CreateSettings();
        var factory = new AzureEventGridClientFactory(Options.Create(settings));

        var publisher = factory.CreatePublisher(settings.Endpoints["orders"]);
        await using var client = factory.CreateServiceBusClient();
        var administrationClient = factory.CreateServiceBusAdministrationClient();

        Assert.NotNull(publisher);
        Assert.Equal("test.servicebus.windows.net", client.FullyQualifiedNamespace);
        Assert.NotNull(administrationClient);
    }

    [Fact]
    public void CreateServiceBusClient_RejectsMissingTransport()
    {
        var factory = new AzureEventGridClientFactory(Options.Create(new AzureEventGridSettings()));

        Assert.Throws<InvalidOperationException>(() => factory.CreateServiceBusClient());
    }

    private static AzureEventGridSettings CreateSettings()
        => new()
        {
            ServiceBusFullyQualifiedNamespace = "test.servicebus.windows.net",
            ServiceBusCredential = new TestTokenCredential(),
            Endpoints = new Dictionary<string, Endpoint>
            {
                ["orders"] = new Endpoint
                {
                    Url = "https://orders.eventgrid.azure.net/api/events",
                    Credential = new TestTokenCredential()
                }
            }
        };
}

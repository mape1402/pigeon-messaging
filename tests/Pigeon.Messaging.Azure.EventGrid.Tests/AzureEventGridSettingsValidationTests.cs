namespace Pigeon.Messaging.Azure.EventGrid.Tests;

using Microsoft.Extensions.Options;

public class AzureEventGridSettingsValidationTests
{
    [Fact]
    public void Validate_RejectsNullSettings()
        => Assert.Throws<ArgumentNullException>(() => AzureEventGridSettingsValidator.Validate(null));

    [Fact]
    public void Validate_AcceptsAccessKeyPublisherAndConnectionStringTransport()
        => AzureEventGridSettingsValidator.Validate(new AzureEventGridSettings
        {
            ServiceBusEndPoint = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=Root;SharedAccessKey=test",
            Endpoints = new Dictionary<string, Endpoint>
            {
                ["orders"] = new Endpoint { Url = "https://orders.eventgrid.azure.net/api/events", AccessKey = "access-key" }
            }
        });

    [Fact]
    public void Validate_AcceptsCredentialPublisherAndServiceBusTransport()
        => AzureEventGridSettingsValidator.Validate(new AzureEventGridSettings
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
        });

    [Fact]
    public void Validate_RejectsAmbiguousEndpointAuthenticationWithoutLeakingAccessKey()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => AzureEventGridSettingsValidator.Validate(new AzureEventGridSettings
        {
            Endpoints = new Dictionary<string, Endpoint>
            {
                ["orders"] = new Endpoint
                {
                    Url = "https://orders.eventgrid.azure.net/api/events",
                    AccessKey = "secret-access-key",
                    Credential = new TestTokenCredential()
                }
            }
        }));

        Assert.DoesNotContain("secret-access-key", exception.Message);
    }

    [Fact]
    public void Validate_RejectsAmbiguousServiceBusAuthentication()
        => Assert.Throws<InvalidOperationException>(() => AzureEventGridSettingsValidator.Validate(new AzureEventGridSettings
        {
            ServiceBusEndPoint = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=Root;SharedAccessKey=test",
            ServiceBusFullyQualifiedNamespace = "test.servicebus.windows.net"
        }));

    [Fact]
    public void Validate_RejectsUndefinedEndpoint()
        => Assert.Throws<InvalidOperationException>(() => AzureEventGridSettingsValidator.Validate(new AzureEventGridSettings
        {
            Endpoints = new Dictionary<string, Endpoint> { ["orders"] = null }
        }));

    [Fact]
    public void Validate_RejectsEndpointWithoutUrl()
        => Assert.Throws<InvalidOperationException>(() => AzureEventGridSettingsValidator.Validate(new AzureEventGridSettings
        {
            Endpoints = new Dictionary<string, Endpoint> { ["orders"] = new Endpoint { AccessKey = "access-key" } }
        }));

    [Fact]
    public void Validate_RejectsEndpointWithoutAuthentication()
        => Assert.Throws<InvalidOperationException>(() => AzureEventGridSettingsValidator.Validate(new AzureEventGridSettings
        {
            Endpoints = new Dictionary<string, Endpoint>
            {
                ["orders"] = new Endpoint { Url = "https://orders.eventgrid.azure.net/api/events" }
            }
        }));

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Validate_RejectsIncompleteServiceBusCredentialMode(bool namespaceConfigured, bool credentialConfigured)
    {
        var settings = new AzureEventGridSettings
        {
            ServiceBusFullyQualifiedNamespace = namespaceConfigured ? "test.servicebus.windows.net" : null,
            ServiceBusCredential = credentialConfigured ? new TestTokenCredential() : null
        };

        Assert.Throws<InvalidOperationException>(() => new AzureEventGridClientFactory(Options.Create(settings)));
    }
}

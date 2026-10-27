namespace Pigeon.Messaging.Azure.ServiceBus.Tests;

using Microsoft.Extensions.Options;

public class AzureServiceBusSettingsValidationTests
{
    [Fact]
    public void Validate_AcceptsConnectionStringMode()
        => AzureServiceBusSettingsValidator.Validate(new AzureServiceBusSettings
        {
            ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=Root;SharedAccessKey=test"
        });

    [Fact]
    public void Validate_AcceptsCredentialMode()
        => AzureServiceBusSettingsValidator.Validate(new AzureServiceBusSettings
        {
            FullyQualifiedNamespace = "test.servicebus.windows.net",
            Credential = new TestTokenCredential()
        });

    [Fact]
    public void Validate_RejectsAmbiguousAuthenticationWithoutLeakingConnectionString()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => AzureServiceBusSettingsValidator.Validate(new AzureServiceBusSettings
        {
            ConnectionString = "secret-connection-string",
            FullyQualifiedNamespace = "test.servicebus.windows.net",
            Credential = new TestTokenCredential()
        }));

        Assert.DoesNotContain("secret-connection-string", exception.Message);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void Validate_RejectsIncompleteCredentialMode(bool namespaceConfigured, bool credentialConfigured)
    {
        var settings = new AzureServiceBusSettings
        {
            FullyQualifiedNamespace = namespaceConfigured ? "test.servicebus.windows.net" : null,
            Credential = credentialConfigured ? new TestTokenCredential() : null
        };

        Assert.Throws<InvalidOperationException>(() => new AzureServiceBusClientFactory(Options.Create(settings)));
    }
}

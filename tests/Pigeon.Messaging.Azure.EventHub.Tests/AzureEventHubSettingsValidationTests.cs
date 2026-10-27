namespace Pigeon.Messaging.Azure.EventHub.Tests;

using Microsoft.Extensions.Options;

public class AzureEventHubSettingsValidationTests
{
    [Fact]
    public void Validate_RejectsNullSettings()
        => Assert.Throws<ArgumentNullException>(() => AzureEventHubSettingsValidator.Validate(null));

    [Fact]
    public void Validate_AcceptsConnectionStringMode()
        => AzureEventHubSettingsValidator.Validate(new AzureEventHubSettings
        {
            ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=Root;SharedAccessKey=test"
        });

    [Fact]
    public void Validate_AcceptsCredentialMode()
        => AzureEventHubSettingsValidator.Validate(new AzureEventHubSettings
        {
            FullyQualifiedNamespace = "test.servicebus.windows.net",
            Credential = new TestTokenCredential()
        });

    [Fact]
    public void Validate_RejectsAmbiguousAuthenticationWithoutLeakingConnectionString()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => AzureEventHubSettingsValidator.Validate(new AzureEventHubSettings
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
        var settings = new AzureEventHubSettings
        {
            FullyQualifiedNamespace = namespaceConfigured ? "test.servicebus.windows.net" : null,
            Credential = credentialConfigured ? new TestTokenCredential() : null
        };

        Assert.Throws<InvalidOperationException>(() => new AzureEventHubClientFactory(Options.Create(settings)));
    }
}

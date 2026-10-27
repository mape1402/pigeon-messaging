namespace Pigeon.Messaging.Azure.ServiceBus.Tests;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Pigeon.Messaging.Consuming.Configuration;

public class ServicesExtensionsAuthenticationTests
{
    [Fact]
    public void UseAzureServiceBus_RejectsMissingAuthenticationAtRegistration()
    {
        var builder = CreateBuilder();

        Assert.Throws<InvalidOperationException>(() => builder.UseAzureServiceBus());
    }

    [Fact]
    public void UseAzureServiceBus_AcceptsProgrammaticCredential()
    {
        var builder = CreateBuilder();

        builder.UseAzureServiceBus(settings =>
        {
            settings.FullyQualifiedNamespace = "test.servicebus.windows.net";
            settings.Credential = new TestTokenCredential();
        });
    }

    private static GlobalSettingsBuilder CreateBuilder()
        => new(
            new ServiceCollection(),
            Substitute.For<IConfiguration>(),
            Substitute.For<IConsumingConfigurator>(),
            new MessagingSettings { MessageBrokers = new() });
}

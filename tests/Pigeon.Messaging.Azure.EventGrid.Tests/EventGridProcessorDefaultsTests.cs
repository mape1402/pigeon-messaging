namespace Pigeon.Messaging.Azure.EventGrid.Tests;

using global::Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
using System.Reflection;

public class EventGridProcessorDefaultsTests
{
    [Fact]
    public void NullConsumerExecution_UsesSdkDefaults()
    {
        var settings = Options.Create(new AzureEventGridSettings { ServiceBusEndPoint = "Endpoint=sb://test/;SharedAccessKeyName=Root;SharedAccessKey=abc" });
        var provider = new EventGridProvider(settings, Options.Create(new GlobalSettings { ConsumerExecution = null }));
        var options = (ServiceBusProcessorOptions)typeof(EventGridProvider).GetMethod("CreateProcessorOptions", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(provider, null);

        Assert.False(options.AutoCompleteMessages);
        Assert.Equal(new ServiceBusProcessorOptions().MaxConcurrentCalls, options.MaxConcurrentCalls);
        Assert.Equal(new ServiceBusProcessorOptions().PrefetchCount, options.PrefetchCount);
    }
}

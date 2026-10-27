namespace Pigeon.Messaging.RabbitMq.Tests;

using Pigeon.Messaging.Rabbit;
using RabbitMQ.Client;

public class RabbitSettingsValidationTests
{
    [Fact]
    public void Validate_AcceptsUrlMode()
        => RabbitSettingsValidator.Validate(new RabbitSettings { Url = "amqps://user:password@rabbit.example/vhost" });

    [Fact]
    public void Validate_AcceptsProgrammaticFactoryMode()
        => RabbitSettingsValidator.Validate(new RabbitSettings
        {
            ConfigureConnectionFactory = _ => { }
        });

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("relative-path")]
    public void Validate_RejectsMissingOrInvalidUrlWithoutFactory(string url)
    {
        var settings = new RabbitSettings { Url = url };

        Assert.Throws<InvalidOperationException>(() => RabbitSettingsValidator.Validate(settings));
    }
}

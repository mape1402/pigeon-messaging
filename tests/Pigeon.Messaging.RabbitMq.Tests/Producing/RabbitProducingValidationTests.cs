namespace Pigeon.Messaging.RabbitMq.Tests.Producing;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Pigeon.Messaging.Rabbit;
using Pigeon.Messaging.Rabbit.Producing;

public class RabbitProducingValidationTests
{
    private readonly IConnectionProvider _connection = Substitute.For<IConnectionProvider>();
    private readonly ISerializer _serializer = Substitute.For<ISerializer>();
    private readonly IOptions<RabbitSettings> _settings = Options.Create(new RabbitSettings());
    private readonly NullLogger<RabbitProducingAdapter> _logger = NullLogger<RabbitProducingAdapter>.Instance;

    [Fact]
    public void Constructor_RequiresConnection() => Assert.Throws<ArgumentNullException>(() => new RabbitProducingAdapter(null, _serializer, _settings, _logger));

    [Fact]
    public void Constructor_RequiresSerializer() => Assert.Throws<ArgumentNullException>(() => new RabbitProducingAdapter(_connection, null, _settings, _logger));

    [Fact]
    public void Constructor_RequiresSettings() => Assert.Throws<ArgumentNullException>(() => new RabbitProducingAdapter(_connection, _serializer, null, _logger));

    [Fact]
    public void Constructor_RequiresSettingsValue()
    {
        var settings = Substitute.For<IOptions<RabbitSettings>>();
        settings.Value.Returns((RabbitSettings)null);
        Assert.Throws<ArgumentNullException>(() => new RabbitProducingAdapter(_connection, _serializer, settings, _logger));
    }

    [Fact]
    public void Constructor_RequiresLogger() => Assert.Throws<ArgumentNullException>(() => new RabbitProducingAdapter(_connection, _serializer, _settings, null));
}

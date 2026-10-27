using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Options;
using NSubstitute;
using Pigeon.Messaging.Consuming.Configuration;
using Pigeon.Messaging.Producing;

namespace Pigeon.Messaging.Kafka.Tests;

public class KafkaTopologyAdapterTests
{
    private readonly IConfigurationProvider _configuration = Substitute.For<IConfigurationProvider>();
    private readonly IAdminClient _admin = Substitute.For<IAdminClient>();
    private readonly IOptions<KafkaSettings> _settings = Options.Create(new KafkaSettings { TopicNumPartitions = 3, TopicReplicationFactor = 2 });

    [Fact]
    public void Constructor_RejectsNullDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new KafkaTopologyAdapter(null, _settings, (_, _) => _admin));
        Assert.Throws<ArgumentNullException>(() => new KafkaTopologyAdapter(_configuration, null, (_, _) => _admin));
        Assert.Throws<ArgumentNullException>(() => new KafkaTopologyAdapter(_configuration, _settings, null));
    }

    [Fact]
    public async Task EnsurePublish_UsesConfiguredTopicParameters()
    {
        var adapter = Adapter();
        await adapter.EnsurePublishTopologyAsync(PublishingRoute.ForTopic("orders"));
        await _admin.Received(1).CreateTopicsAsync(Arg.Is<IEnumerable<TopicSpecification>>(topics =>
            topics.Single().Name == "orders" && topics.Single().NumPartitions == 3 && topics.Single().ReplicationFactor == 2),
            Arg.Any<CreateTopicsOptions>());
    }

    [Fact]
    public async Task EnsureConsume_UsesEndpointTopic()
    {
        var adapter = Adapter();
        await adapter.EnsureConsumeTopologyAsync(new ConsumerEndpoint("orders", "billing"));
        await _admin.Received(1).CreateTopicsAsync(Arg.Is<IEnumerable<TopicSpecification>>(topics => topics.Single().Name == "orders"),
            Arg.Any<CreateTopicsOptions>());
    }

    [Fact]
    public async Task EnsureTopic_IgnoresAlreadyExistingTopic()
    {
        _admin.CreateTopicsAsync(Arg.Any<IEnumerable<TopicSpecification>>(), Arg.Any<CreateTopicsOptions>())
            .Returns(Task.FromException(new CreateTopicsException(new List<CreateTopicReport> { new() { Topic = "orders", Error = new Error(ErrorCode.TopicAlreadyExists) } })));
        await Adapter().EnsureConsumeTopologyAsync(new ConsumerEndpoint("orders"));
    }

    [Fact]
    public async Task EnsureTopic_PropagatesOtherFailures()
    {
        _admin.CreateTopicsAsync(Arg.Any<IEnumerable<TopicSpecification>>(), Arg.Any<CreateTopicsOptions>())
            .Returns(Task.FromException(new CreateTopicsException(new List<CreateTopicReport> { new() { Topic = "orders", Error = new Error(ErrorCode.TopicAuthorizationFailed) } })));
        await Assert.ThrowsAsync<CreateTopicsException>(() => Adapter().EnsureConsumeTopologyAsync(new ConsumerEndpoint("orders")));
    }

    [Fact]
    public void BrokerName_IsKafka() => Assert.Equal("Kafka", Adapter().BrokerName);

    [Fact]
    public async Task EnsureTopic_ForwardsOAuthRefreshHandlerToAdministrativeClient()
    {
        Action<IClient, string> handler = (_, _) => { };
        _configuration.GetOAuthBearerTokenRefreshHandler().Returns(handler);
        Action<IClient, string> observed = null;
        var adapter = new KafkaTopologyAdapter(_configuration, _settings, (_, refreshHandler) =>
        {
            observed = refreshHandler;
            return _admin;
        });

        await adapter.EnsurePublishTopologyAsync(PublishingRoute.ForTopic("orders"));

        Assert.Same(handler, observed);
    }

    private KafkaTopologyAdapter Adapter() => new(_configuration, _settings, (_, _) => _admin);
}

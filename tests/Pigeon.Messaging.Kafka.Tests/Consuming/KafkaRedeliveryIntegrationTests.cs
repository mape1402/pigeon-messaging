using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Pigeon.Messaging.Consuming.Configuration;
using Pigeon.Messaging.Consuming.Management;
using Pigeon.Messaging.Kafka.Consuming;

namespace Pigeon.Messaging.Kafka.Tests.Consuming;

public class KafkaRedeliveryIntegrationTests
{
    [Fact]
    public async Task Retry_ReplaysFailedAndLaterMessagesAfterOutOfOrderCompletion()
    {
        var bootstrap = Environment.GetEnvironmentVariable("PIGEON_KAFKA_TEST_BOOTSTRAP");
        if (string.IsNullOrWhiteSpace(bootstrap))
            return;

        var topic = "pigeon-observer-" + Guid.NewGuid().ToString("N");
        using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = bootstrap }).Build();
        await admin.CreateTopicsAsync(new[] { new TopicSpecification { Name = topic, NumPartitions = 1, ReplicationFactor = 1 } });
        try
        {
            using var producer = new ProducerBuilder<Null, string>(new ProducerConfig { BootstrapServers = bootstrap }).Build();
            await producer.ProduceAsync(topic, new Message<Null, string> { Value = "0" });
            await producer.ProduceAsync(topic, new Message<Null, string> { Value = "1" });

            var configurator = Substitute.For<IConsumingConfigurator>();
            configurator.GetAllEndpoints().Returns(new[] { new ConsumerEndpoint(topic) });
            var configuration = Substitute.For<IConfigurationProvider>();
            configuration.GetConsumerConfig().Returns(new ConsumerConfig
            {
                BootstrapServers = bootstrap,
                GroupId = "pigeon-observer-" + Guid.NewGuid().ToString("N"),
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false
            });
            var adapter = new KafkaConsumingAdapter(configurator, configuration, Options.Create(new GlobalSettings()), NullLogger<KafkaConsumingAdapter>.Instance);
            var first = new TaskCompletionSource<MessageConsumedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
            var second = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var replayed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var firstCount = 0;
            var secondCount = 0;
            adapter.MessageConsumedAsync += async (_, message, _) =>
            {
                if (message.RawPayload == "0" && Interlocked.Increment(ref firstCount) == 1)
                    first.TrySetResult(message);
                else if (message.RawPayload == "1" && Interlocked.Increment(ref secondCount) == 1)
                {
                    await message.CompleteAsync();
                    second.TrySetResult();
                }
                else
                {
                    await message.CompleteAsync();
                    if (message.RawPayload == "1")
                        replayed.TrySetResult();
                }
            };

            await adapter.StartConsumeAsync();
            try
            {
                var firstMessage = await first.Task.WaitAsync(TimeSpan.FromSeconds(30));
                await second.Task.WaitAsync(TimeSpan.FromSeconds(30));
                await firstMessage.RetryAsync();
                await replayed.Task.WaitAsync(TimeSpan.FromSeconds(30));
                Assert.Equal(2, firstCount);
                Assert.Equal(2, secondCount);
            }
            finally
            {
                await adapter.StopConsumeAsync();
            }
        }
        finally
        {
            await admin.DeleteTopicsAsync(new[] { topic });
        }
    }
}

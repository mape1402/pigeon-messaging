using NSubstitute;
using Pigeon.Messaging.Contracts;
using Pigeon.Messaging.Producing;
using Pigeon.Messaging.Producing.Management;
using Microsoft.Extensions.Options;

namespace Pigeon.Messaging.Tests.Producing
{
    public class ProducerTests
    {
        [Fact]
        public void Constructor_Should_Throw_If_Deps_Null()
        {
            var manager = Substitute.For<IProducingManager>();
            var settings = new GlobalSettings { Domain = "test" };
            var interceptors = Enumerable.Empty<IPublishInterceptor>();
            var options = Options.Create(settings);

            Assert.Throws<ArgumentNullException>(() => new Producer(null, manager, options));
            Assert.Throws<ArgumentNullException>(() => new Producer(interceptors, null, options));
            Assert.Throws<ArgumentNullException>(() => new Producer(interceptors, manager, null));
        }

        [Fact]
        public async Task PublishAsync_Should_Call_PublishCore()
        {
            var interceptor = Substitute.For<IPublishInterceptor>();
            var manager = Substitute.For<IProducingManager>();
            var settings = new GlobalSettings { Domain = "test-domain" };
            var options = Options.Create(settings);

            var producer = new Producer(new[] { interceptor }, manager, options);

            await producer.PublishAsync("msg", "topic");

            await interceptor.Received(1).Intercept(Arg.Any<PublishContext>());
            await manager.Received(1).PushAsync(Arg.Any<WrappedPayload<string>>(), "topic", CancellationToken.None);
        }

        [Fact]
        public async Task PublishAsync_Should_Throw_If_Message_Null()
        {
            var producer = GetProducer();
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await producer.PublishAsync<string>(null, "topic"));
        }

        [Fact]
        public async Task PublishAsync_Should_Throw_If_Topic_Empty()
        {
            var producer = GetProducer();
            await Assert.ThrowsAsync<ArgumentException>(async () => await producer.PublishAsync("msg", ""));
        }

        private Producer GetProducer()
        {
            var interceptor = Substitute.For<IPublishInterceptor>();
            var manager = Substitute.For<IProducingManager>();
            var settings = new GlobalSettings { Domain = "test" };
            var options = Options.Create(settings);
            return new Producer(new[] { interceptor }, manager, options);
        }
    }
}

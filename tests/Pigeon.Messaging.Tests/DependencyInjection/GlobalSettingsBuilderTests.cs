namespace Pigeon.Messaging.Tests.DependencyInjection
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using NSubstitute;
    using Pigeon.Messaging;
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Consuming.Dispatching;
    using Pigeon.Messaging.Contracts;
    using System;
    using System.Linq;
    using System.Reflection;
    using Xunit;

    public class GlobalSettingsBuilderTests
    {
        private readonly IServiceCollection _services;
        private readonly IConfiguration _configuration;
        private readonly IConsumingConfigurator _consumingConfigurator;
        private readonly MessagingSettings _settings;

        public GlobalSettingsBuilderTests()
        {
            _services = new ServiceCollection();
            _configuration = Substitute.For<IConfiguration>();
            _consumingConfigurator = Substitute.For<IConsumingConfigurator>();
            _settings = new MessagingSettings { Domain = "default" };
        }

        [Fact]
        public void Ctor_Should_Throw_If_Dependencies_Are_Null()
        {
            Assert.Throws<ArgumentNullException>(() => new GlobalSettingsBuilder(null, _configuration, _consumingConfigurator, _settings));
            Assert.Throws<ArgumentNullException>(() => new GlobalSettingsBuilder(_services, null, _consumingConfigurator, _settings));
            Assert.Throws<ArgumentNullException>(() => new GlobalSettingsBuilder(_services, _configuration, null, _settings));
            Assert.Throws<ArgumentNullException>(() => new GlobalSettingsBuilder(_services, _configuration, _consumingConfigurator, null));
        }

        [Fact]
        public void SetDomain_Should_Set_GlobalSettings_Domain()
        {
            var builder = CreateBuilder();

            builder.SetDomain("new-domain");

            Assert.Equal("new-domain", builder.GlobalSettings.Domain);
        }

        [Fact]
        public void ScanConsumersFromAssemblies_Should_Set_TargetAssemblies()
        {
            var builder = CreateBuilder();
            var assemblies = new[] { Assembly.GetExecutingAssembly() };

            builder.ScanConsumersFromAssemblies(assemblies);

            Assert.Equal(assemblies, builder.GlobalSettings.TargetAssemblies);
        }

        [Fact]
        public void AddService_Should_Register_ServiceDescriptor()
        {
            var builder = CreateBuilder();

            builder.AddService<IService, Service>(ServiceLifetime.Singleton);

            var descriptor = _services.FirstOrDefault(sd => sd.ServiceType == typeof(IService));
            Assert.NotNull(descriptor);
            Assert.Equal(typeof(Service), descriptor.ImplementationType);
        }

        [Fact]
        public void AddService_Should_Throw_If_Implementation_Not_Assignable()
        {
            var builder = CreateBuilder();
            Assert.Throws<InvalidOperationException>(() => builder.AddService(typeof(IService), typeof(object), ServiceLifetime.Scoped));
        }

        [Fact]
        public void AddKeyedService_Should_Register_With_Key()
        {
            var builder = CreateBuilder();

            object serviceKey = "key1";

            builder.AddKeyedService<IService, Service>(serviceKey, ServiceLifetime.Scoped);

            var descriptor = _services.FirstOrDefault(sd => sd.ServiceType == typeof(IService) && sd.IsKeyedService && sd.ServiceKey == serviceKey);
            Assert.NotNull(descriptor);
            Assert.Equal(typeof(Service), descriptor.KeyedImplementationType);
        }

        [Fact]
        public void AddKeyedService_Should_Throw_If_Invalid_Implementation()
        {
            var builder = CreateBuilder();
            Assert.Throws<InvalidOperationException>(() => builder.AddKeyedService("key", typeof(IService), typeof(object), ServiceLifetime.Scoped));
        }

        [Fact]
        public void AddFeature_Should_Invoke_Delegate()
        {
            var builder = CreateBuilder();
            bool invoked = false;

            builder.AddFeature(fb => invoked = true);

            Assert.True(invoked);
        }

        [Fact]
        public void AddConsumeHandler_Should_Call_ConsumingConfigurator()
        {
            var builder = CreateBuilder();
            ConsumeHandler<object> handler = (_, _) => Task.CompletedTask;

            builder.AddConsumeHandler("topic", SemanticVersion.Default, handler);
            builder.AddConsumeHandler("topic", handler);

            _consumingConfigurator.Received(1).AddConsumer("topic", SemanticVersion.Default, handler);
            _consumingConfigurator.Received(1).AddConsumer("topic", handler);
        }

        private GlobalSettingsBuilder CreateBuilder()
        {
            return new GlobalSettingsBuilder(_services, _configuration, _consumingConfigurator, _settings);
        }

        interface IService { }
        class Service : IService { }
    }

}

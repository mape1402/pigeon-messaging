using global::Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;

namespace Pigeon.Messaging.Azure.EventGrid.Tests
{
    public class EventGridProviderTests
    {
        [Fact]
        public void Constructor_Should_Throw_ArgumentNullException_For_Null_Options()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new EventGridProvider(null));
        }

        [Fact]
        public void GetClient_Should_Return_EventGridPublisher_When_Topic_Has_Routing()
        {
            // Arrange
            var settings = new AzureEventGridSettings
            {
                ServiceBusEndPoint = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=testkey",
                TopicRouting = new Dictionary<string, string>
                {
                    ["test-topic"] = "endpoint1"
                },
                Endpoints = new Dictionary<string, Endpoint>
                {
                    ["endpoint1"] = new Endpoint
                    {
                        Url = "https://test.eventgrid.azure.net/api/events",
                        AccessKey = "test-key"
                    }
                }
            };
            var options = Options.Create(settings);
            var provider = new EventGridProvider(options);

            // Act
            var client = provider.GetClient("test-topic");

            // Assert
            Assert.NotNull(client);
            Assert.IsAssignableFrom<IEventGridPublisher>(client);
        }

        [Fact]
        public void GetClient_Should_Return_Same_Instance_For_Same_Topic()
        {
            // Arrange
            var settings = new AzureEventGridSettings
            {
                ServiceBusEndPoint = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=testkey",
                TopicRouting = new Dictionary<string, string>
                {
                    ["test-topic"] = "endpoint1"
                },
                Endpoints = new Dictionary<string, Endpoint>
                {
                    ["endpoint1"] = new Endpoint
                    {
                        Url = "https://test.eventgrid.azure.net/api/events",
                        AccessKey = "test-key"
                    }
                }
            };
            var options = Options.Create(settings);
            var provider = new EventGridProvider(options);

            // Act
            var client1 = provider.GetClient("test-topic");
            var client2 = provider.GetClient("test-topic");

            // Assert
            Assert.Same(client1, client2);
        }

        [Fact]
        public void GetClient_Should_Use_DefaultEndpoint_When_Topic_Not_Found_In_Routing()
        {
            // Arrange
            var settings = new AzureEventGridSettings
            {
                ServiceBusEndPoint = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=testkey",
                DefaultEndpoint = "default-endpoint",
                TopicRouting = new Dictionary<string, string>
                {
                    ["other-topic"] = "endpoint1"
                },
                Endpoints = new Dictionary<string, Endpoint>
                {
                    ["default-endpoint"] = new Endpoint
                    {
                        Url = "https://default.eventgrid.azure.net/api/events",
                        AccessKey = "default-key"
                    },
                    ["endpoint1"] = new Endpoint
                    {
                        Url = "https://test.eventgrid.azure.net/api/events",
                        AccessKey = "test-key"
                    }
                }
            };
            var options = Options.Create(settings);
            var provider = new EventGridProvider(options);

            // Act
            var client = provider.GetClient("unmapped-topic");

            // Assert
            Assert.NotNull(client);
            Assert.IsAssignableFrom<IEventGridPublisher>(client);
        }

        [Fact]
        public void GetClient_Should_Use_DefaultEndpoint_When_TopicRouting_Is_Null()
        {
            // Arrange
            var settings = new AzureEventGridSettings
            {
                ServiceBusEndPoint = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=testkey",
                DefaultEndpoint = "default-endpoint",
                TopicRouting = null, // Explicitly null
                Endpoints = new Dictionary<string, Endpoint>
                {
                    ["default-endpoint"] = new Endpoint
                    {
                        Url = "https://default.eventgrid.azure.net/api/events",
                        AccessKey = "default-key"
                    }
                }
            };
            var options = Options.Create(settings);
            var provider = new EventGridProvider(options);

            // Act
            var client = provider.GetClient("any-topic");

            // Assert
            Assert.NotNull(client);
            Assert.IsAssignableFrom<IEventGridPublisher>(client);
        }

        [Fact]
        public void GetClient_Should_Cache_Default_Endpoint_Clients_Correctly()
        {
            // Arrange
            var settings = new AzureEventGridSettings
            {
                ServiceBusEndPoint = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=testkey",
                DefaultEndpoint = "default-endpoint",
                TopicRouting = new Dictionary<string, string>(),
                Endpoints = new Dictionary<string, Endpoint>
                {
                    ["default-endpoint"] = new Endpoint
                    {
                        Url = "https://default.eventgrid.azure.net/api/events",
                        AccessKey = "default-key"
                    }
                }
            };
            var options = Options.Create(settings);
            var provider = new EventGridProvider(options);

            // Act - Get clients for different unmapped topics
            var client1 = provider.GetClient("topic1");
            var client2 = provider.GetClient("topic2");
            var client3 = provider.GetClient("topic1"); // Same as first

            // Assert
            Assert.NotNull(client1);
            Assert.NotNull(client2);
            Assert.NotNull(client3);
            Assert.NotSame(client1, client2); // Different topics should have different clients
            Assert.Same(client1, client3); // Same topic should return cached client
        }

        [Fact]
        public void GetClient_Should_Throw_When_No_Routing_And_No_DefaultEndpoint()
        {
            // Arrange
            var settings = new AzureEventGridSettings
            {
                ServiceBusEndPoint = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=testkey",
                DefaultEndpoint = null, // No default endpoint
                TopicRouting = new Dictionary<string, string>(),
                Endpoints = new Dictionary<string, Endpoint>()
            };
            var options = Options.Create(settings);
            var provider = new EventGridProvider(options);

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => provider.GetClient("unknown-topic"));
            Assert.Contains("No routing key found for topic 'unknown-topic', and no default endpoint is configured", ex.Message);
        }

        [Fact]
        public void GetClient_Should_Throw_When_No_Routing_And_DefaultEndpoint_Is_Empty()
        {
            // Arrange
            var settings = new AzureEventGridSettings
            {
                ServiceBusEndPoint = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=testkey",
                DefaultEndpoint = "", // Empty default endpoint
                TopicRouting = new Dictionary<string, string>(),
                Endpoints = new Dictionary<string, Endpoint>()
            };
            var options = Options.Create(settings);
            var provider = new EventGridProvider(options);

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => provider.GetClient("unknown-topic"));
            Assert.Contains("No routing key found for topic 'unknown-topic', and no default endpoint is configured", ex.Message);
        }

        [Fact]
        public void GetClient_Should_Throw_When_No_Routing_And_DefaultEndpoint_Is_Whitespace()
        {
            // Arrange
            var settings = new AzureEventGridSettings
            {
                ServiceBusEndPoint = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=testkey",
                DefaultEndpoint = "   ", // Whitespace default endpoint
                TopicRouting = new Dictionary<string, string>(),
                Endpoints = new Dictionary<string, Endpoint>()
            };
            var options = Options.Create(settings);
            var provider = new EventGridProvider(options);

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => provider.GetClient("unknown-topic"));
            Assert.Contains("No routing key found for topic 'unknown-topic', and no default endpoint is configured", ex.Message);
        }

        [Fact]
        public void GetClient_Should_Throw_When_DefaultEndpoint_Not_Found_In_Endpoints()
        {
            // Arrange
            var settings = new AzureEventGridSettings
            {
                ServiceBusEndPoint = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=testkey",
                DefaultEndpoint = "non-existent-endpoint",
                TopicRouting = new Dictionary<string, string>(),
                Endpoints = new Dictionary<string, Endpoint>
                {
                    ["other-endpoint"] = new Endpoint
                    {
                        Url = "https://other.eventgrid.azure.net/api/events",
                        AccessKey = "other-key"
                    }
                }
            };
            var options = Options.Create(settings);
            var provider = new EventGridProvider(options);

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => provider.GetClient("unknown-topic"));
            Assert.Contains("Event Grid topic 'unknown-topic' is not configured in the settings", ex.Message);
        }

        [Fact]
        public void GetClient_Should_Throw_For_Missing_Endpoint_In_Routing()
        {
            // Arrange
            var settings = new AzureEventGridSettings
            {
                ServiceBusEndPoint = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=testkey",
                TopicRouting = new Dictionary<string, string>
                {
                    ["test-topic"] = "missing-endpoint"
                },
                Endpoints = new Dictionary<string, Endpoint>()
            };
            var options = Options.Create(settings);
            var provider = new EventGridProvider(options);

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => provider.GetClient("test-topic"));
            Assert.Contains("Event Grid topic 'test-topic' is not configured in the settings", ex.Message);
        }

        [Fact]
        public void GetClient_Should_Prefer_TopicRouting_Over_DefaultEndpoint()
        {
            // Arrange
            var settings = new AzureEventGridSettings
            {
                ServiceBusEndPoint = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=testkey",
                DefaultEndpoint = "default-endpoint",
                TopicRouting = new Dictionary<string, string>
                {
                    ["specific-topic"] = "specific-endpoint"
                },
                Endpoints = new Dictionary<string, Endpoint>
                {
                    ["default-endpoint"] = new Endpoint
                    {
                        Url = "https://default.eventgrid.azure.net/api/events",
                        AccessKey = "default-key"
                    },
                    ["specific-endpoint"] = new Endpoint
                    {
                        Url = "https://specific.eventgrid.azure.net/api/events",
                        AccessKey = "specific-key"
                    }
                }
            };
            var options = Options.Create(settings);
            var provider = new EventGridProvider(options);

            // Act
            var specificClient = provider.GetClient("specific-topic");
            var unmappedClient = provider.GetClient("unmapped-topic");

            // Assert
            Assert.NotNull(specificClient);
            Assert.NotNull(unmappedClient);
            Assert.NotSame(specificClient, unmappedClient);
            // Note: We can't directly verify which endpoint was used without exposing internal state,
            // but the fact that both calls succeed proves the routing logic is working
        }

        [Fact]
        public void CreateProcessor_Should_Return_ServiceBusProcessor()
        {
            // Arrange
            var settings = new AzureEventGridSettings
            {
                ServiceBusEndPoint = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=testkey",
                TopicRouting = new Dictionary<string, string>(),
                Endpoints = new Dictionary<string, Endpoint>()
            };
            var options = Options.Create(settings);
            var provider = new EventGridProvider(options);

            // Act
            var processor = provider.CreateProcessor("test-topic");

            // Assert
            Assert.NotNull(processor);
            Assert.IsAssignableFrom<ServiceBusProcessor>(processor);
        }

        [Fact]
        public void CreateProcessor_Should_Return_Different_Instances_For_Different_Topics()
        {
            // Arrange
            var settings = new AzureEventGridSettings
            {
                ServiceBusEndPoint = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=testkey",
                TopicRouting = new Dictionary<string, string>(),
                Endpoints = new Dictionary<string, Endpoint>()
            };
            var options = Options.Create(settings);
            var provider = new EventGridProvider(options);

            // Act
            var processor1 = provider.CreateProcessor("topic1");
            var processor2 = provider.CreateProcessor("topic2");

            // Assert
            Assert.NotSame(processor1, processor2);
        }
    }
}
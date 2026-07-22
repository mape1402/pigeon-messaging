# Pigeon.Messaging

**Simple. Fast. Broker-agnostic messaging for .NET.**

[![Build](https://github.com/mape1402/pigeon-messaging/actions/workflows/publish.yaml/badge.svg)](https://github.com/mape1402/pigeon-messaging/actions/workflows/publish.yaml)
[![NuGet](https://img.shields.io/nuget/v/Pigeon.Messaging.svg)](https://www.nuget.org/packages/Pigeon.Messaging/)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

---

**Pigeon** is a lightweight, extensible library for .NET that abstracts integration with messaging systems like RabbitMQ, Kafka, Azure Service Bus, Azure Event Grid, and Azure Event Hub.

Its goal is to simplify publishing and consuming messages through a unified, decoupled API, so you can switch message brokers without rewriting your business logic.

---

## Features

- **Consistent API** for multiple message brokers.
- **Fluent configuration** through `IServiceCollection`.
- **Publish and consume workflows** with topic and semantic-version support.
- **Consumer discovery** through `HubConsumer` and `ConsumerAttribute`.
- **Publish and consume interceptors** for metadata, tracing, security context, sagas, and other cross-cutting behavior.
- **Broker adapters** that keep business code independent from the transport.
- **Lightweight core package** with adapter packages for each broker.

Pigeon is a good fit for microservices, distributed architectures, and applications that need reliable asynchronous communication without coupling domain code to a specific broker SDK.

## Supported Brokers

- RabbitMQ
- Kafka
- Azure Service Bus
- Azure Event Grid
- Azure Event Hub

---

## Installation

Install the core package and one or more broker adapters:

```bash
dotnet add package Pigeon.Messaging
dotnet add package Pigeon.Messaging.Rabbit
dotnet add package Pigeon.Messaging.Kafka
dotnet add package Pigeon.Messaging.Azure.ServiceBus
dotnet add package Pigeon.Messaging.Azure.EventGrid
dotnet add package Pigeon.Messaging.Azure.EventHub
```

## Quick Start

### Configure Pigeon

Register Pigeon in your `Program.cs` or `Startup.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pigeon.Messaging;
using Pigeon.Messaging.Rabbit;
using System.Text.Json;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddPigeon(builder.Configuration, config =>
    {
        config.SetDomain("YourApp.Domain")
              .UseRabbitMq(rabbit =>
              {
                  rabbit.Url = "amqp://guest:guest@localhost:5672";
              });
    })
    .ConfigureJsonOptions(options =>
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

var app = builder.Build();
await app.RunAsync();
```

### Define a Consumer

Create a message contract and register a handler:

```csharp
public class HelloWorldMessage
{
    public string Text { get; set; }
}

builder.Services
    .AddPigeon(builder.Configuration, config =>
    {
        config.SetDomain("YourApp.Domain")
              .UseRabbitMq();
    })
    .AddConsumeHandler<HelloWorldMessage>(
        topic: "hello-world",
        version: "1.0.0",
        handler: (context, message) =>
        {
            return Task.CompletedTask;
        });
```

You can also group related consumers in a `HubConsumer` and register them by scanning assemblies:

```csharp
public class CreateUserMessage { }
public class UpdateUserMessage { }
public class UpdateUserV2Message { }

public class UserHubConsumer : HubConsumer
{
    private readonly IAnyService _service;

    public UserHubConsumer(IAnyService service)
    {
        _service = service;
    }

    [Consumer("create-user", "1.0.0")]
    public Task CreateUser(CreateUserMessage message, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    [Consumer("update-user", "1.0.0")]
    [Consumer("update-user", "1.0.1")]
    public Task UpdateUser(UpdateUserMessage message, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    [Consumer("update-user", "2.0.0")]
    public Task UpdateUserV2(UpdateUserV2Message message, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
```

Register consumers discovered in an assembly:

```csharp
builder.Services.AddPigeon(builder.Configuration, config =>
{
    config.ScanConsumersFromAssemblies(typeof(UserHubConsumer).Assembly)
          .UseRabbitMq();
});
```

### Publish a Message

Resolve `IProducer` and publish a message to a topic:

```csharp
var producer = app.Services.GetRequiredService<IProducer>();

await producer.PublishAsync(
    new HelloWorldMessage { Text = "Hello, Pigeon!" },
    topic: "hello-world");
```

### Add Interceptors

Interceptors let you attach and read metadata around publishing and consuming.

```csharp
public class TraceMetadata
{
    public string CorrelationId { get; set; }
}

public class TracePublishInterceptor : IPublishInterceptor
{
    public ValueTask Intercept(PublishContext context, CancellationToken cancellationToken = default)
    {
        context.AddMetadata("Trace", new TraceMetadata
        {
            CorrelationId = Guid.NewGuid().ToString("N")
        });

        return ValueTask.CompletedTask;
    }
}

public class TraceConsumeInterceptor : IConsumeInterceptor
{
    public ValueTask Intercept(ConsumeContext context, CancellationToken cancellationToken = default)
    {
        var trace = context.GetMetadata<TraceMetadata>("Trace");
        return ValueTask.CompletedTask;
    }
}
```

Register interceptors after calling `AddPigeon`:

```csharp
builder.Services
    .AddPigeon(builder.Configuration, config =>
    {
        config.UseRabbitMq();
    })
    .AddConsumeInterceptor<TraceConsumeInterceptor>()
    .AddPublishInterceptor<TracePublishInterceptor>();
```

### Sample `appsettings.json`

```json
{
  "Pigeon": {
    "Domain": "YourApp.Domain",
    "MessageBrokers": {
      "RabbitMq": {
        "Url": "amqp://guest:guest@localhost:5672"
      },
      "Kafka": {
        "BootstrapServers": "localhost:9092",
        "UserName": "test",
        "Password": "test",
        "SecurityProtocol": "PlainText",
        "SaslMechanism": "Plain",
        "Acks": "All"
      },
      "AzureServiceBus": {
        "ConnectionString": "Endpoint=sb://test/;SharedAccessKeyName=Root;SharedAccessKey=abc"
      },
      "AzureEventGrid": {
        "ServiceBusEndpoint": "",
        "Endpoints": {
          "Greeting": {
            "Url": "https://example.eventgrid.azure.net/api/events",
            "AccessKey": "event-grid-access-key"
          },
          "Users": {
            "Url": "https://example-users.eventgrid.azure.net/api/events",
            "AccessKey": "event-grid-access-key"
          }
        },
        "TopicRouting": {
          "commands.demo.hello-world": "Greeting",
          "events.demo.user-created": "Users"
        }
      },
      "AzureEventHub": {
        "ConnectionString": "Endpoint=sb://tests/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc"
      }
    }
  }
}
```

## Extensible by Design

- Pluggable broker adapters.
- Automatic consumer scanning by `ConsumerAttribute`.
- Built-in support for message versioning and interceptors.
- Clean separation of concerns through `ConsumingManager`, `ProducingManager`, adapters, and interceptors.

## Upcoming Features

- **Enhanced Management Capabilities** Add health checking, multi-publishing, multi-consuming, failover and more.
- **Support for Amazon SQS and Mosquitto** Add adapters for Amazon SQS and Mosquitto message brokers.

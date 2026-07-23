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
- **Raw message publishing** when a broker payload should be sent without the default Pigeon wrapper.
- **Routed publishing** for broker-native fan-out patterns such as RabbitMQ exchanges, routing keys, queues, and bindings.
- **Consumer discovery** through `HubConsumer` and `ConsumerAttribute`.
- **Publish and consume interceptors** for metadata, tracing, security context, sagas, and other cross-cutting behavior.
- **Configurable topology provisioning** to create broker infrastructure on startup, publish, consume, or leave it fully manual.
- **Configurable acknowledgement behavior** with manual ack, auto-ack on receive, or ack after a successful handler.
- **Broker adapters** that keep business code independent from the transport.
- **In-memory broker** for unit tests, examples, and modular monolith scenarios.
- **Lightweight core package** with adapter packages for each broker.

Pigeon is a good fit for microservices, distributed architectures, and applications that need reliable asynchronous communication without coupling domain code to a specific broker SDK.

## Supported Brokers

- RabbitMQ
- Kafka
- Azure Service Bus
- Azure Event Grid
- Azure Event Hub
- In-memory

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
dotnet add package Pigeon.Messaging.InMemory
dotnet add package Pigeon.Messaging.EntityFrameworkCore
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

### Publish a Raw Message

Use raw publishing when you want to send the payload directly to the broker without the default wrapped Pigeon envelope:

```csharp
await producer.PublishRawAsync(
    new HelloWorldMessage { Text = "Hello, Pigeon!" },
    topic: "hello-world");
```

### Route a Message to Multiple Consumers

Adapters that support broker-side routing can publish one message and deliver it to multiple configured consumers. In RabbitMQ, for example, one publish can target an exchange and routing key while each consumer owns its queue and binding:

```csharp
config.SetTopologyProvisioningMode(
        TopologyProvisioningMode.OnStartup |
        TopologyProvisioningMode.OnPublish |
        TopologyProvisioningMode.OnConsume)
      .UseRabbitMq(rabbit =>
      {
          rabbit.Url = "amqp://guest:guest@localhost:5672";
          rabbit.Exchange = "orders.exchange";
          rabbit.ExchangeType = "direct";
      });

pigeon.AddConsumeHandler<OrderCreatedMessage>(
    topic: "orders.created",
    version: "1.0.0",
    subscription: "billing.orders.created",
    handler: (context, message) => Task.CompletedTask);

pigeon.AddConsumeHandler<OrderCreatedMessage>(
    topic: "orders.created",
    version: "1.0.0",
    subscription: "audit.orders.created",
    handler: (context, message) => Task.CompletedTask);

await producer.PublishAsync(
    new OrderCreatedMessage(),
    topic: "orders.exchange",
    routingKey: "orders.created",
    version: "1.0.0");
```

The Rabbit sample includes a runnable end-to-end version with one exchange, one routing key, two queues, and two bindings:

```bash
dotnet run --project samples/Pigeon.Messaging.Rabbit.Sample/Pigeon.Messaging.Rabbit.Sample.csproj
```

### Use the In-Memory Broker

Use the in-memory broker for tests, samples, or modular monoliths where messages should stay inside the current process:

```csharp
builder.Services
    .AddPigeon(builder.Configuration, config =>
    {
        config.UseInMemoryBroker();
    })
    .AddConsumeHandler<OrderCreatedMessage>(
        topic: "orders.created",
        version: "1.0.0",
        subscription: "billing-module",
        handler: (context, message) => Task.CompletedTask)
    .AddConsumeHandler<OrderCreatedMessage>(
        topic: "orders.created",
        version: "1.0.0",
        subscription: "audit-module",
        handler: (context, message) => Task.CompletedTask);

await producer.PublishAsync(new OrderCreatedMessage(), "orders.created");
```

One publish is delivered to every matching in-memory subscription. The broker is process-local, non-durable, and not distributed, so it is not a replacement for RabbitMQ, Kafka, or Azure brokers between services.

Tests can inspect the broker state:

```csharp
var broker = serviceProvider.GetRequiredService<IInMemoryBroker>();

Assert.Single(broker.PublishedMessages);
Assert.Equal(2, broker.Deliveries.Count);
```

Run the in-memory sample:

```bash
dotnet run --project samples/Pigeon.Messaging.InMemory.Sample/Pigeon.Messaging.InMemory.Sample.csproj
```

### Configure Topology Provisioning

Pigeon defaults to manual topology provisioning, so infrastructure is expected to already exist unless configured otherwise. You can combine provisioning modes when your topology is partly known at startup and partly dynamic at runtime:

```csharp
config.SetTopologyProvisioningMode(
    TopologyProvisioningMode.OnStartup |
    TopologyProvisioningMode.OnPublish |
    TopologyProvisioningMode.OnConsume);
```

- `Manual`: Pigeon only publishes and consumes.
- `OnStartup`: creates known topology when the app starts.
- `OnPublish`: creates publish topology when a dynamic publish route appears.
- `OnConsume`: creates consume topology when a dynamic consumer appears.

Pigeon keeps an in-memory registry of provisioned topology so the same queue, topic, subscription, exchange, or binding is not recreated on every publish or consume.

### Configure Consumer Acknowledgements

Consumer acknowledgements are configured globally. The default is `Manual`, which means Pigeon does not ack automatically:

```csharp
config.ConfigureConsumerExecution(execution =>
{
    execution.AcknowledgementMode = MessageAcknowledgementMode.Manual;
    execution.MaxConcurrency = 8;
    execution.QueueCapacity = 256;
    execution.HandlerTimeout = TimeSpan.FromSeconds(30);
});
```

Available acknowledgement modes:

- `Manual`: the handler controls acknowledgement through `ConsumeContext.CompleteAsync()` or `ConsumeContext.FailAsync(...)`.
- `OnReceive`: the adapter uses broker auto-ack behavior where available.
- `OnHandlerSuccess`: Pigeon acknowledges only after the handler completes successfully.

Manual acknowledgement works from consumer methods and hub consumers:

```csharp
pigeon.AddConsumeHandler<HelloWorldMessage>(
    topic: "hello-world",
    version: "1.0.0",
    handler: async (context, message) =>
    {
        await DoWorkAsync(message);
        await context.CompleteAsync();
    });
```

### Access the Current Consume Context

Use `IConsumeContextAccessor` when application services need to read the current `ConsumeContext` without receiving it directly as a method argument:

```csharp
public class CurrentMessageTenantProvider
{
    private readonly IConsumeContextAccessor _consumeContextAccessor;

    public CurrentMessageTenantProvider(IConsumeContextAccessor consumeContextAccessor)
    {
        _consumeContextAccessor = consumeContextAccessor;
    }

    public string GetTenantId()
    {
        var context = _consumeContextAccessor.ConsumeContext;
        return context?.GetMetadata<string>("tenantId");
    }
}
```

`ConsumeContext` is only available while Pigeon is running consume interceptors or the consumer handler for the current message. Outside a consume pipeline, the accessor returns `null`.

### Configure the Transactional Outbox

The Entity Framework Core outbox plugs into the producer pipeline. `PublishAsync` still runs publish interceptors in the current scope, builds the final `WrappedPayload`, and then stores that exact payload in the outbox instead of sending it directly to the broker. The background dispatcher later publishes the stored payload without running interceptors again.

This keeps scoped metadata, tracing, tenant data, and other publish interceptor output exactly as it existed at publish time. The dispatch step is intentionally separated from the original request scope.

Register the application `DbContext` first, then enable the Pigeon EF outbox:

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(connectionString);
});

builder.Services.AddPigeon(builder.Configuration, config =>
{
    config.UseRabbitMq();

    config.UseEntityFrameworkOutbox<AppDbContext>(outbox =>
    {
        outbox.SchemaMode = OutboxSchemaMode.AutoCreate;
        outbox.DispatchInterval = TimeSpan.FromSeconds(5);
        outbox.ImmediateDispatch = true;
        outbox.DispatchQueueCapacity = 1000;
        outbox.CleanInterval = TimeSpan.FromMinutes(10);
        outbox.PublishedMessageRetention = TimeSpan.FromDays(1);
        outbox.DispatchBatchSize = 50;
        outbox.MaxRetries = 10;
    });
});
```

Pigeon adds its outbox entity to the EF model automatically, so the application `DbContext` does not need a `DbSet` or manual `OnModelCreating` code for Pigeon. With the default `AutoCreate` schema mode, Pigeon creates the outbox table when the app starts for supported relational providers.

Use `OutboxSchemaMode.Manual` when your database schema is created by migrations or another deployment process:

```csharp
config.UseEntityFrameworkOutbox<AppDbContext>(outbox =>
{
    outbox.SchemaMode = OutboxSchemaMode.Manual;
});
```

When `ImmediateDispatch` is enabled, `PublishAsync` persists the outbox message immediately and queues it for background dispatch. If an ambient `TransactionScope` exists, Pigeon waits for that transaction to commit before queuing the message. If the transaction rolls back, nothing is queued and the stored row rolls back with the transaction.

`DispatchInterval` is a recovery interval, not the happy path. It periodically scans the database for pending or retryable messages and puts them back into the in-memory queue if the immediate dispatch path failed or the process restarted.

Without an ambient transaction, the message is stored and queued immediately:

```csharp
await producer.PublishAsync(
    new OrderCreatedMessage { OrderId = order.Id },
    topic: "orders.created");
```

With an ambient transaction, the outbox write participates in that transaction and dispatch starts only after commit:

```csharp
using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

dbContext.Orders.Add(order);
await dbContext.SaveChangesAsync();

await producer.PublishAsync(
    new OrderCreatedMessage { OrderId = order.Id },
    topic: "orders.created");

scope.Complete();
```

The EF outbox storage uses its own `DbContext` instance so it does not flush pending application changes by accident. Transactional consistency with the application work is provided by the ambient transaction, so the selected database provider must support `TransactionScope`.

Raw messages are supported too:

```csharp
await producer.PublishRawAsync(
    new ExternalAuditMessage { Id = auditId },
    topic: "external.audit");
```

Run the transaction sample to see the expected commit and rollback behavior without requiring a broker:

```bash
dotnet run --project samples/Pigeon.Messaging.TransactionScope.Sample/Pigeon.Messaging.TransactionScope.Sample.csproj
```

### Inspect Outbox State

When the EF outbox is registered, Pigeon also exposes `IOutboxDiagnostics` so an application can build health checks, dashboards, or support endpoints without querying the outbox table directly:

```csharp
public class OutboxHealthProbe
{
    private readonly IOutboxDiagnostics _diagnostics;

    public OutboxHealthProbe(IOutboxDiagnostics diagnostics)
    {
        _diagnostics = diagnostics;
    }

    public async Task<OutboxDiagnosticsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        return await _diagnostics.GetSnapshotAsync(cancellationToken);
    }
}
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

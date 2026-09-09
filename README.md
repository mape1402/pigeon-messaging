# Pigeon.Messaging

**Simple. Fast. Broker-agnostic messaging for .NET.**

[![Build](https://github.com/mape1402/pigeon-messaging/actions/workflows/build-and-release.yml/badge.svg)](https://github.com/mape1402/pigeon-messaging/actions/workflows/build-and-release.yml)
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
- **Consume execution interceptors** for wrapping the actual handler globally or by route.
- **Configurable topology provisioning** to create broker infrastructure on startup, publish, consume, or leave it fully manual.
- **Configurable acknowledgement behavior** with manual ack, auto-ack on receive, or ack after a successful handler.
- **Broker adapters** that keep business code independent from the transport.
- **In-memory broker** for unit tests, examples, and modular monolith scenarios.
- **Mule-backed transactional outbox** for durable broker dispatch with retry, recovery, cleanup, and diagnostics.
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

## Supported Frameworks

Pigeon 3.1 supports:

- .NET 8
- .NET 9
- .NET 10

---

## Installation

Install the core package, one broker adapter, and any optional outbox providers you need:

```bash
dotnet add package Pigeon.Messaging
dotnet add package Pigeon.Messaging.Rabbit
dotnet add package Pigeon.Messaging.Kafka
dotnet add package Pigeon.Messaging.Azure.ServiceBus
dotnet add package Pigeon.Messaging.Azure.EventGrid
dotnet add package Pigeon.Messaging.Azure.EventHub
dotnet add package Pigeon.Messaging.InMemory
dotnet add package Pigeon.Testing
dotnet add package Pigeon.Messaging.Outbox.EntityFrameworkCore
dotnet add package Pigeon.Messaging.Outbox.InMemory
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

### Publish Inside an Ambient Transaction

When `PublishAsync` runs inside a `TransactionScope` and the transactional outbox is not enabled, Pigeon suppresses the ambient transaction for the direct broker publish by default. This keeps brokers that do not participate in the current transaction from trying to enlist in it:

```csharp
builder.Services.AddPigeon(builder.Configuration, config =>
{
    config.ConfigurePublishing(publishing =>
    {
        publishing.AmbientTransactionBehavior =
            AmbientTransactionPublishBehavior.SuppressTransaction;
    });
});
```

Suppressing the transaction means the broker publish is not atomic with the surrounding business transaction. If the message must be consistent with database changes, enable the transactional outbox instead.

Use `Throw` when you want Pigeon to fail fast if direct broker publishing happens inside an ambient transaction:

```csharp
config.ConfigurePublishing(publishing =>
{
    publishing.AmbientTransactionBehavior =
        AmbientTransactionPublishBehavior.Throw;
});
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

### Use Route Keys

Use `PigeonRouteKey` to centralize topic, version, and subscription values for runtime configuration:

```csharp
public static class OrderRoutes
{
    public const string CreatedTopic = "orders.created";
    public const string CreatedVersion = "1.0.0";
    public const string BillingSubscription = "billing-module";

    public static readonly PigeonRouteKey CreatedForBilling =
        new(CreatedTopic, CreatedVersion, BillingSubscription);
}
```

Use constants for attributes because C# attributes require compile-time values:

```csharp
[Consumer(
    OrderRoutes.CreatedTopic,
    OrderRoutes.CreatedVersion,
    Subscription = OrderRoutes.BillingSubscription)]
public Task Handle(OrderCreatedMessage message)
{
    return Task.CompletedTask;
}
```

Use the route key in fluent configuration:

```csharp
pigeon.AddConsumeHandler<OrderCreatedMessage>(
    OrderRoutes.CreatedForBilling,
    (context, message) => Task.CompletedTask);
```

String overloads remain available for dynamic scenarios.

### Use Decision Interceptors

Decision interceptors let normal routing decisions happen without throwing exceptions for control flow.

Consume decision interceptors run after existing `IConsumeInterceptor` instances and before the consumer handler:

```csharp
public sealed class InboxDecisionInterceptor : IConsumeDecisionInterceptor
{
    public ValueTask<PigeonConsumeDecisionResult> InterceptAsync(
        ConsumeContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.ExecutionSource == ConsumeExecutionSource.DeferredReplay)
            return ValueTask.FromResult(PigeonConsumeDecisionResult.Continue);

        return ValueTask.FromResult(new PigeonConsumeDecisionResult(
            PigeonConsumeDecision.Defer,
            "The message was persisted for deferred execution."));
    }
}
```

Register decision interceptors globally or for one route:

```csharp
pigeon.AddConsumeDecisionInterceptor<GlobalConsumePolicy>();

pigeon.ForConsumer(OrderRoutes.CreatedForBilling)
    .AddConsumeDecisionInterceptor<InboxDecisionInterceptor>();
```

Consume decisions map to portable settlement:

- `Continue`: executes the handler.
- `AckAndSkip`: skips the handler and acknowledges the broker delivery.
- `Retry`: skips the handler and asks the broker to retry or requeue.
- `Reject`: skips the handler and rejects or dead-letters where supported.
- `Defer`: skips the handler and acknowledges after the interceptor has persisted durable work.

Publish decision interceptors run after existing `IPublishInterceptor` instances and before outbox or direct broker publishing:

```csharp
public sealed class PublishPolicy : IPublishDecisionInterceptor
{
    public ValueTask<PigeonPublishDecisionResult> InterceptAsync(
        PublishContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(new PigeonPublishDecisionResult(PigeonPublishDecision.Continue)
        {
            Metadata = new Dictionary<string, object>
            {
                ["correlation-id"] = Guid.NewGuid().ToString("N")
            }
        });
}
```

Available publish decisions are `Continue`, `Skip`, `Reject`, `UseOutbox`, and `PublishNow`.

### Use Consume Execution Interceptors

Consume execution interceptors wrap the actual consumer handler. Use them when work must happen immediately before and after the handler, including timing, scoped logging, metrics, unit-of-work boundaries, auditing, and failure observation.

```csharp
public sealed class HandlerTimingInterceptor : IConsumeExecutionInterceptor
{
    private readonly ILogger<HandlerTimingInterceptor> _logger;

    public HandlerTimingInterceptor(ILogger<HandlerTimingInterceptor> logger)
    {
        _logger = logger;
    }

    public async ValueTask InvokeAsync(
        ConsumeContext context,
        ConsumeExecutionDelegate next,
        CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            await next(context, cancellationToken);
            _logger.LogInformation("Consumed {Topic} successfully.", context.Topic);
        }
        finally
        {
            var elapsed = Stopwatch.GetElapsedTime(startedAt);
            _logger.LogInformation("Consumer handler elapsed {Elapsed}.", elapsed);
        }
    }
}
```

Register execution interceptors globally or for one route:

```csharp
pigeon.AddConsumeExecutionInterceptor<HandlerTimingInterceptor>();

pigeon.ForConsumer(OrderRoutes.CreatedForBilling)
    .AddConsumeExecutionInterceptor<BillingUnitOfWorkInterceptor>();
```

Execution interceptors run after consume interceptors and consume decision interceptors that return `Continue`. They are skipped when a decision interceptor returns `AckAndSkip`, `Retry`, `Reject`, or `Defer` because the handler is not executed. Deferred replay through `IPigeonConsumerInvoker` uses the same execution pipeline.

### Replay a Consumed Message

Pigeon can capture a consume context as a transport-neutral envelope and invoke the same consumer pipeline later:

```csharp
public sealed class DeferredConsumeInterceptor : IConsumeDecisionInterceptor
{
    private readonly IPigeonConsumeEnvelopeFactory _envelopeFactory;
    private readonly IDurableScheduler _scheduler;

    public DeferredConsumeInterceptor(
        IPigeonConsumeEnvelopeFactory envelopeFactory,
        IDurableScheduler scheduler)
    {
        _envelopeFactory = envelopeFactory;
        _scheduler = scheduler;
    }

    public async ValueTask<PigeonConsumeDecisionResult> InterceptAsync(
        ConsumeContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.ExecutionSource == ConsumeExecutionSource.DeferredReplay)
            return PigeonConsumeDecisionResult.Continue;

        var envelope = _envelopeFactory.Create(context);
        await _scheduler.ScheduleAsync(envelope, cancellationToken);

        return new PigeonConsumeDecisionResult(PigeonConsumeDecision.Defer);
    }
}
```

A background worker can replay the envelope without depending on the original broker delivery:

```csharp
public sealed class DeferredConsumeWorker
{
    private readonly IPigeonConsumerInvoker _consumerInvoker;

    public DeferredConsumeWorker(IPigeonConsumerInvoker consumerInvoker)
    {
        _consumerInvoker = consumerInvoker;
    }

    public async Task ExecuteAsync(PigeonConsumeEnvelope envelope, CancellationToken cancellationToken)
    {
        await _consumerInvoker.InvokeAsync(envelope, cancellationToken);
    }
}
```

The invoker creates a new DI scope, rebuilds `ConsumeContext`, restores `IConsumeContextAccessor`, resolves the same route, and executes the same registered handler or `HubConsumer` method through the consume execution interceptor pipeline.

### Test Pigeon Without a Broker

Use `Pigeon.Testing` when tests need to inspect producers, consumers, message dispatch, metadata, retries, and failure paths without RabbitMQ, Kafka, Azure Service Bus, or a full application host:

```csharp
services.AddPigeonTesting();
services.AddPigeonTestingConsumers(typeof(CustomersHubConsumer).Assembly);
```

Publish messages into the in-memory testing transport and dispatch them when the test is ready:

```csharp
var pigeon = serviceProvider.GetRequiredService<IPigeonTestingTransport>();
var customerId = Guid.NewGuid();

await pigeon.PublishAsync(new CustomerCreatedMessage(customerId));

pigeon.ShouldContainMessage<CustomerCreatedMessage>(
    message => message.CustomerId == customerId);

await pigeon.DispatchPendingAsync();

pigeon.ShouldContainConsumedMessage<CustomerCreatedMessage>(
    message => message.CustomerId == customerId);
```

`PublishAsync` uses the real Pigeon producer pipeline, so publish interceptors can enrich the payload before the testing transport captures it:

```csharp
var message = pigeon.ShouldContainMessage<CustomerCreatedMessage>();
message.Headers["correlation-id"].ShouldBe(correlationId);
message.CorrelationId.ShouldBe(correlationId);
```

Failure paths can be simulated without touching broker SDKs:

```csharp
pigeon.FailNext<CustomerCreatedMessage>(new TimeoutException());

await pigeon.PublishAsync(new CustomerCreatedMessage(customerId));
await pigeon.DispatchPendingAsync();

pigeon.ShouldContainDeadLetterMessage<CustomerCreatedMessage>();
pigeon.ShouldHaveConsumerFailure<CustomerCreatedMessage>();
```

External test hosts can expose a thin wrapper over the adapter-friendly registration:

```csharp
services.AddPigeonTestingAdapter(typeof(CustomersHubConsumer).Assembly);
```

### Use the In-Memory Outbox

Use the in-memory outbox provider for tests and samples that need the real Pigeon outbox pipeline without a database. This provider uses Mule's in-memory durable action engine under the Pigeon outbox API:

```csharp
builder.Services.AddPigeon(builder.Configuration, config =>
{
    config.UseInMemoryBroker();
    config.UseInMemoryOutbox();
});
```

The provider stores durable actions in the current process and exposes `IInMemoryOutbox` for assertions:

```csharp
var outbox = serviceProvider.GetRequiredService<IInMemoryOutbox>();

Assert.Single(outbox.Messages);
```

It is process-local and non-durable. Use `Pigeon.Messaging.Outbox.EntityFrameworkCore` for production durability.

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

For high-throughput publishers, avoid first-message topology latency by warming known publish routes during startup:

```csharp
config.SetTopologyProvisioningMode(
    TopologyProvisioningMode.OnStartup |
    TopologyProvisioningMode.OnPublish);

config.PreProvisionPublishRoutes(
    PublishingRoute.ForExchange("events", "orders.created"),
    PublishingRoute.ForExchange("events", "orders.cancelled"));
```

`OnPublish` is useful for dynamic routes, but known hot-path routes should be pre-provisioned when possible.

### Configure Consumer Acknowledgements

Consumer acknowledgements are configured globally. The default is `Manual`, which means Pigeon does not ack automatically:

```csharp
config.ConfigureConsumerExecution(execution =>
{
    execution.AcknowledgementMode = MessageAcknowledgementMode.Manual;
    execution.HandlerTimeout = TimeSpan.FromSeconds(30);
});
```

Available acknowledgement modes:

- `Manual`: the handler controls acknowledgement through `ConsumeContext.CompleteAsync()` or `ConsumeContext.FailAsync(...)`.
- `OnReceive`: the adapter uses broker auto-ack behavior where available.
- `OnHandlerSuccess`: Pigeon acknowledges only after the handler completes successfully.

By default, Pigeon does not cap consumer dispatch concurrency based on CPU cores. Delivery is broker-driven and handlers are dispatched as messages arrive. If an application needs to protect a dependency such as a database, HTTP API, or downstream service, set an explicit limit:

```csharp
config.ConfigureConsumerExecution(execution =>
{
    execution.MaxConcurrency = 128;
    execution.QueueCapacity = 10_000;
    execution.PrefetchCount = 128;
});
```

When `MaxConcurrency` and `QueueCapacity` are both `null` or less than `1`, Pigeon does not create an internal dispatch queue; broker adapters dispatch directly into the consumer pipeline. If either setting is configured, Pigeon enables the internal dispatch queue so it can apply concurrency control and backpressure before handlers run.

RabbitMQ prefetch uses `ConsumerExecution.PrefetchCount` when configured. If `PrefetchCount` is not configured but `MaxConcurrency` is configured, RabbitMQ derives prefetch from `MaxConcurrency`. With `OnReceive`, RabbitMQ uses auto-ack and Pigeon does not apply QoS.

Azure Service Bus and Azure Event Grid consumption map the same common settings into Azure processor options: `MaxConcurrency` becomes `MaxConcurrentCalls`, and `PrefetchCount` becomes the processor `PrefetchCount`. Kafka concurrency remains partition-driven by the Kafka consumer group and assigned partitions; Pigeon does not fake queue-style parallelism on top of Kafka partitions.

For productive high-throughput defaults, opt in explicitly:

```csharp
config.ConfigureHighThroughputConsumers();
```

That helper sets a bounded `MaxConcurrency`, bounded `QueueCapacity`, `PrefetchCount`, and keeps the existing handler timeout unless one is provided:

```csharp
config.ConfigureHighThroughputConsumers(
    concurrencyMultiplier: 8,
    queueCapacityMultiplier: 100,
    handlerTimeout: TimeSpan.FromMinutes(2));
```

Consumer backlog can be inspected through `IConsumerExecutionDiagnostics`:

```csharp
var snapshot = diagnostics.GetSnapshot();

Console.WriteLine(snapshot.QueuedMessages);
Console.WriteLine(snapshot.ActiveHandlers);
Console.WriteLine(snapshot.AverageQueueWait);
```

Broker adapters accept consumed messages through an async consume contract, so bounded `QueueCapacity` applies async backpressure instead of blocking broker callback threads with sync-over-async calls.

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

The transactional outbox plugs into the producer pipeline. `PublishAsync` still runs publish interceptors in the current scope, builds the final `WrappedPayload`, and then stores that exact payload in the outbox instead of sending it directly to the broker. Pigeon stores the publish intent as a Mule durable action and Mule handles retry, recovery scanning, immediate dispatch, and cleanup.

Pigeon 2.8 uses Mule Durable Actions 1.4.1 for the outbox providers, including Mule's bounded dispatch and execution queues, lane-aware runtime settings, and high-throughput durable action improvements.

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
        outbox.DispatchQueueCapacity = 100_000;
        outbox.ExecutionQueueCapacity = 50_000;
        outbox.CleanInterval = TimeSpan.FromMinutes(10);
        outbox.PublishedMessageRetention = TimeSpan.FromDays(1);
        outbox.DispatchBatchSize = 500;
        outbox.WorkerCount = Environment.ProcessorCount;
        outbox.MaxDegreeOfParallelism = Environment.ProcessorCount * 8;
        outbox.MaxDrainBatchesPerCycle = 8;
        outbox.MaxDrainActionsPerCycle = 10_000;
        outbox.DrainUntilEmpty = true;
        outbox.MaxRetries = 10;
    });
});
```

`DispatchQueueCapacity` controls how many durable actions can wait for Mule dispatch. `ExecutionQueueCapacity` controls how many actions can wait for execution after they have been locked and accepted by Mule's executor. Both can be configured globally or per outbox lane.

For the common high-throughput profile, use the outbox helper:

```csharp
config.UseEntityFrameworkOutbox<AppDbContext>(outbox =>
{
    outbox.ConfigureHighThroughput();
});
```

Pigeon adds Mule's durable action entity to the EF model automatically, so the application `DbContext` does not need a `DbSet` or manual `OnModelCreating` code for Pigeon. The schema can be created with EF migrations, `EnsureCreated`, or your normal database deployment process.

Use `OutboxSchemaMode.Manual` when your database schema is created by migrations or another deployment process:

```csharp
config.UseEntityFrameworkOutbox<AppDbContext>(outbox =>
{
    outbox.SchemaMode = OutboxSchemaMode.Manual;
});
```

When `ImmediateDispatch` is enabled, `PublishAsync` persists the outbox message as a Mule durable action and queues it for background dispatch. If an ambient `TransactionScope` exists, dispatch waits until the transaction commits. If the transaction rolls back, the durable action rolls back with it and nothing is dispatched.

`DispatchInterval` is a recovery interval, not the happy path. Mule periodically scans for pending or retryable actions and puts them back into the in-memory dispatch queue if the immediate dispatch path failed or the process restarted.

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

The EF outbox uses its own `DbContext` instance so it does not flush pending application changes by accident. Transactional consistency with the application work is provided by the ambient transaction, so the selected database provider must support `TransactionScope`.

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

When an outbox provider is registered, Pigeon exposes `IOutboxDiagnostics` so an application can build health checks, dashboards, or support endpoints without querying Mule's durable action table directly:

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

The snapshot includes durable state and Mule runtime metrics such as pending, locked, completed, failed, throughput per minute, backlog by lane, completed per minute by lane, runtime failures, and dispatch latency averages when the selected Mule provider reports them.

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

Use `IConsumeExecutionInterceptor` when the code must wrap the actual handler instead of only inspecting metadata before routing decisions complete.

### Sample `appsettings.json`

```json
{
  "Pigeon": {
    "Domain": "YourApp.Domain",
    "ConsumerExecution": {
      "AcknowledgementMode": "OnHandlerSuccess",
      "MaxConcurrency": 256,
      "QueueCapacity": 10000,
      "PrefetchCount": 256,
      "HandlerTimeout": "00:02:00"
    },
    "Outbox": {
      "Enabled": true,
      "ImmediateDispatch": true,
      "DispatchQueueCapacity": 100000,
      "ExecutionQueueCapacity": 50000,
      "DispatchBatchSize": 500,
      "WorkerCount": 16,
      "MaxDegreeOfParallelism": 128,
      "MaxDrainBatchesPerCycle": 8,
      "MaxDrainActionsPerCycle": 10000,
      "DrainUntilEmpty": true,
      "DispatchInterval": "00:00:05"
    },
    "MessageBrokers": {
      "RabbitMq": {
        "Url": "amqp://guest:guest@localhost:5672",
        "PublisherChannelPoolSize": 16
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

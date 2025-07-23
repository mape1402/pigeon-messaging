# 🕊️ Pigeon.Messaging

**Simple. Fast. Broker-agnostic messaging for .NET**

[![Build](https://github.com/mape1402/pigeon-messaging/actions/workflows/publish.yaml/badge.svg)](https://github.com/mape1402/pigeon-messaging/actions/workflows/publish.yaml)
[![NuGet](https://img.shields.io/nuget/v/Pigeon.Messaging.svg)](https://www.nuget.org/packages/Pigeon.Messaging/)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

---

**Pigeon** is a lightweight, extensible library for .NET (or your chosen platform) that abstracts integration with messaging systems like RabbitMQ, Kafka, Azure Service Bus, and more. Its goal is to simplify publishing and consuming messages through a unified, decoupled API, so you can switch message brokers without rewriting your business logic.

---

## ✨ Features

- ✅ **Consistent API** for multiple message brokers
- ⚙️ **Fluent and flexible configuration**
- 📬 **Supports common messaging patterns** 
- 🔌 **Easily extensible** with adapters for new brokers
- 🪶 **Lightweight** with minimal dependencies

**Pigeon** is perfect for microservices, distributed architectures, and any application that needs reliable asynchronous communication.

### 🛠️ Supported Brokers

- Rabbit MQ
- Kafka 
- Azure Service Bus

---

## 📦 Installation

```bash
dotnet add package Pigeon.Messaging
dotnet add package Pigeon.Messaging.RabbitMq // Or any Message Broker Adapter
dotnet add package Pigeon.Messaging.Kafka 
dotnet add package Pigeon.Messaging.Azure.ServiceBus 
```

## 🚀 Quick Start

### ⚙️ Configure Pigeon

Register the Pigeon infrastructure in your `Program.cs` (or `Startup.cs`):

```c#
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pigeon.Messaging;
using Pigeon.Messaging.Rabbit;
using System.Reflection;

var builder = Host.CreateApplicationBuilder(args);

// Register Pigeon with RabbitMQ
builder.Services.AddPigeon(builder.Configuration, config =>
{
    config.SetDomain("YourApp.Domain")
          .UseRabbitMq(rabbit =>
          {
              rabbit.Url = "amqp://guest:guest@localhost:5672";
          });
});

// Build and run your host
var app = builder.Build();
await app.RunAsync();
```

### 📨 Define a Consumer

Create a message contract and a consumer handler:

```c#
public class HelloWorldMessage { }

builder.Services.AddPigeon(builder.Configuration, config =>
{
    ...
})
.AddConsumer<HelloWorldMessage>("hello-world", "1.0.0", (context, message) => {
    // Do something with the message ...
    return Task.CompletedTask;
})
```

A simple way to create and register your consumers is by using `HubConsumer`. This way, you can group related consumers into a single class.

```c#
public class CreateUserMessage { }
public class UpdateUserMessage {}
public class UpdateUserV2Message {}

public class UserHubConsumer : HubConsumer{
    
    // Support Dependency Injection
    public UserHubConsumer(IAnyService service){
      //...
      // Consuming Context Access by property Context
	 Console.WriteLine($"Received new message. Topic: {Context.Topic} Version: {Context.MessageVersion} From: {Context.From}");
    }
    
    // Easy consumer by attribute declaration
    [Consumer("create-user", "1.0.0")]
    public Task CreateUser(CreateUserMessage message, CancellationToken cancellationToken = default){
       //DO something ...
	  return Task.CompletedTask;
    }
    
    // Support for multiple versioning or topics
    [Consumer("update-user", "1.0.1")]
    [Consumer("update-user", "1.0.0")] 
    public Task UpdateUser(UpdateUserMessage message, CancellationToken cancellationToken = default){
      //DO something ...
	  return Task.CompletedTask;
    }

    // Support for multiple consumers by version 
	[Consumer("update-user", "2.0.0")]
    public Task UpdateUserV2(UpdateUserV2Message message, CancellationToken cancellationToken = default){
      //DO something ...
	  return Task.CompletedTask;
    }
}
```

Simple registration for Dependency Injection:

```c#
builder.Services.AddPigeon(builder.Configuration, config =>
{
    config.ScanConsumersFromAssemblies(typeof(UserHubConsumer).Assembly);
});
```

### 📤 Publish a Message

Resolve the `IProducer` and send a message:

```c#
var producer = app.Services.GetRequiredService<IProducer>();

await producer.PublishAsync(new MyMessage { Text = "Hello, Pigeon!" }, topic: "my-topic");
```

### 🛡️ Add Interceptors (Optional)

Pigeon lets you add interceptors to run logic before or after producing/consuming:

```c#
public class MyMetadata{
    public string SomeValue { get; set; }
}

public class MyPublishInterceptor : IPublishInterceptor{
    
    // Support Dependency Injection
    public MyPublishInterceptor(IMyService service){
        
    }
    
    public ValueTask Intercept(PublishContext context, CancellationToken cancellationToken = default){	   
      // Do something...
      var myMetadata = new MyMetadata{
        SomeValue = "Attach extra information to your messages such as tracing, SAGAS information, security information, etc."  
      };
        
      context.AddMetadata("MyMetadata", myMetadata);
        
      return ValueTask.CompletedTask;
    }
}

public class MyConsumeInterceptor : IConsumeInterceptor{
    
    // Support Dependency Injection
    public MyConsumeInterceptor(IMyService service){
        
    }
    
    public ValueTask Intercept(ConsumeContext context, CancellationToken cancellationToken = default){
      var myMetadata = context.GetMetadata<MyMetadata>("MyMetadata");
	   
      // Do something...
        
      return ValueTask.CompletedTask;
    }
}
```

```C#
builder.Services.AddPigeon(builder.Configuration, config =>
{
    config.UseRabbitMq();

    // Add custom interceptors
})
.AddConsumeInterceptor<MyConsumeInterceptor>()
.AddPublishInterceptor<MyPublishInterceptor>();
```

### 📚 Sample `appsettings.json`

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
        }
    }  
  }
}
```

### 🧩 Extensible by Design

✅ Pluggable broker adapters (RabbitMQ by default)
 ✅ Automatic consumer scanning by `ConsumerAttribute`
 ✅ Built-in support for versioning and interceptors
 ✅ Clean separation of concerns: `ConsumingManager`, `ProducingManager`, adapters, interceptors

## 🛠️ Upcoming Features

- **Enhanced Management Capabilities** Add health checking, multi-publishing, multi-consuming, failover and more.
- **Support for Amazon SQS and Mosquitto** Add adapters for Amazon SQS and Mosquitto message brokers.

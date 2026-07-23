# Pigeon Rabbit sample

This sample runs an end-to-end RabbitMQ scenario with:

- one exchange
- one routing key
- two queues
- two bindings
- one published message delivered to both consumers

The RabbitMQ connection string is stored with .NET User Secrets and is not committed.

## Configure

```powershell
dotnet user-secrets set "RabbitMq:Url" "<rabbit-connection-string>" --project samples\Pigeon.Messaging.Rabbit.Sample\Pigeon.Messaging.Rabbit.Sample.csproj
```

## Run

```powershell
dotnet run --project samples\Pigeon.Messaging.Rabbit.Sample\Pigeon.Messaging.Rabbit.Sample.csproj
```

The sample creates unique queues and a unique routing key per run so the topology can be inspected after it completes.

## Acknowledgement modes

The sample defaults to `OnHandlerSuccess`, which confirms each Rabbit message after the consumer handler completes successfully.

```powershell
dotnet run --project samples\Pigeon.Messaging.Rabbit.Sample\Pigeon.Messaging.Rabbit.Sample.csproj -- --Sample:AcknowledgementMode OnHandlerSuccess
```

Use `Manual` to confirm messages from inside the consumer handler with `ConsumeContext.CompleteAsync()`.

```powershell
dotnet run --project samples\Pigeon.Messaging.Rabbit.Sample\Pigeon.Messaging.Rabbit.Sample.csproj -- --Sample:AcknowledgementMode Manual
```

Use `OnReceive` to let Rabbit consume with `autoAck`.

```powershell
dotnet run --project samples\Pigeon.Messaging.Rabbit.Sample\Pigeon.Messaging.Rabbit.Sample.csproj -- --Sample:AcknowledgementMode OnReceive
```

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

The worker also demonstrates `IConsumeContextAccessor`: before publishing there is no consume context, while each consumer resolves a scoped service that reads the current subscription from the accessor.

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

## Outbox mode

Use `Sample:UseOutbox` to persist the final Pigeon payload in an EF Core SQLite outbox before it is dispatched to Rabbit.

In this mode the sample demonstrates the current Pigeon outbox design:

- `PublishAsync` still executes publish interceptors and creates the final Pigeon payload.
- Pigeon stores that payload as a Mule durable action in SQLite.
- Mule queues the durable action for immediate background dispatch.
- If dispatch fails or the process restarts, Mule recovers pending/retryable actions with the configured interval.
- `IOutboxDiagnostics` reports pending, locked, published, and failed outbox actions without querying the table directly.

```powershell
dotnet run --project samples\Pigeon.Messaging.Rabbit.Sample\Pigeon.Messaging.Rabbit.Sample.csproj -- --Sample:UseOutbox true --Sample:AcknowledgementMode OnHandlerSuccess
```

The console logs include the SQLite database path, the durable action count, and the outbox diagnostics snapshot.

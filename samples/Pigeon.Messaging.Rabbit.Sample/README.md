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

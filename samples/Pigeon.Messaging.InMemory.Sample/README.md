# Pigeon in-memory broker sample

This sample demonstrates Pigeon's in-memory broker adapter for tests, examples, and modular monolith scenarios.

It publishes one `OrderCreatedMessage` and delivers it to two independent subscriptions:

- `billing-module`
- `audit-module`

No external broker is required.

## Run

```powershell
dotnet run --project samples\Pigeon.Messaging.InMemory.Sample\Pigeon.Messaging.InMemory.Sample.csproj
```

The console output shows the published message count, the delivery count, and acknowledgement state for each subscription.

# Pigeon in-memory broker sample

This sample demonstrates Pigeon's in-memory broker adapter for tests, examples, modular monolith scenarios, and deferred consume replay.

It publishes one `OrderCreatedMessage` and delivers it to two independent subscriptions:

- `billing-module`
- `audit-module`

The `audit-module` consumer runs inline from the broker delivery. The `billing-module` route has a consume decision interceptor that captures a `PigeonConsumeEnvelope`, returns `Defer`, acknowledges the broker delivery, and later replays the same consumer through `IPigeonConsumerInvoker`.

The sample also shows:

- centralized route keys with `PigeonRouteKey`
- route-specific consume decision interceptors
- route-specific consume execution interceptors around the handler
- global publish decision interceptors
- replay using the same registered consume handler
- `IConsumeContextAccessor` compatibility during replay

No external broker is required.

## Run

```powershell
dotnet run --project samples\Pigeon.Messaging.InMemory.Sample\Pigeon.Messaging.InMemory.Sample.csproj
```

The console output shows the inline delivery, deferred replay, audit execution interceptor events, published message count, delivery count, and acknowledgement state for each subscription.

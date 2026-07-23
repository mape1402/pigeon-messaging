# Pigeon TransactionScope outbox sample

This sample demonstrates the happy path for Pigeon's transactional outbox with an ambient `TransactionScope`:

- a message published inside a committed transaction is persisted to the outbox and dispatched from the in-memory queue
- a message published inside a rolled back transaction is not persisted and is not dispatched

The sample uses an in-memory transactional outbox storage and the official Pigeon in-memory broker, so it does not require a database or external broker.

## Run

```powershell
dotnet run --project samples\Pigeon.Messaging.TransactionScope.Sample\Pigeon.Messaging.TransactionScope.Sample.csproj
```

The console output shows the outbox state before publishing, after the committed publish, after background dispatch, and after the rolled back publish.

# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

------

## [v4.0.0] - 2026-09-10

### Added

- `PigeonPublishEnvelope` for persisting prepared publish operations outside Pigeon.
- `IPigeonPublishEnvelopeFactory` for external outbox integrations that need the final post-interceptor payload, route, metadata, headers, correlation id, trace id, and raw publish flag.
- `IPigeonPublisherInvoker` for replaying prepared publish envelopes without rerunning producer interceptors, publish decision interceptors, or internal outbox logic.
- Public publish context metadata, headers, correlation id, trace id, content type, operation, and transport values for durable integration adapters.

### Deprecated

- Pigeon's internal outbox registration APIs in favor of SquirrelBox Outbox through `SquirrelBox.Messaging.Pigeon`.

------

## [v3.1.0] - 2026-09-09

### Added

- `IConsumeExecutionInterceptor` and `ConsumeExecutionDelegate` for wrapping consumer handler execution.
- Global consume execution interceptor registration through `AddConsumeExecutionInterceptor<TInterceptor>()`.
- Route-specific consume execution interceptor registration through `ForConsumer(...).AddConsumeExecutionInterceptor<TInterceptor>()`.
- Shared consume handler pipeline for live broker deliveries, deferred replay, direct handlers, and `HubConsumer` methods.

------

## [v3.0.0] - 2026-09-09

### Added

- `PigeonRouteKey` for reusable topic, version, and subscription route declarations across consume and publish configuration.
- Global and route-specific consume decision interceptors through `IConsumeDecisionInterceptor`.
- Consume decisions for `Continue`, `AckAndSkip`, `Reject`, `Retry`, and `Defer`.
- Global and route-specific publish decision interceptors through `IPublishDecisionInterceptor`.
- Publish decisions for `Continue`, `Skip`, `Reject`, `UseOutbox`, and `PublishNow`.
- Replayable consume envelopes through `PigeonConsumeEnvelope` and `IPigeonConsumeEnvelopeFactory`.
- Public deferred consume invocation through `IPigeonConsumerInvoker`.
- `ConsumeExecutionSource` to distinguish live broker delivery, deferred replay, and manual invocation.
- Portable settlement operations for complete, retry, and reject.
- Reply headers and reply metadata collections on `ConsumeContext`.

### Changed

- Consume dispatch now evaluates decision interceptors before handlers, allowing normal control flow without exceptions.
- Publish dispatch now evaluates decision interceptors after existing publish interceptors and before outbox or broker dispatch.
- RabbitMQ and Azure Service Bus consumers now map retry and reject decisions to broker-native settlement operations where available.

------

## [v2.8.0] - 2026-08-18

### Changed

- Consumer dispatch now bypasses Pigeon's internal dispatch queue by default when no `MaxConcurrency` or `QueueCapacity` is configured.
- Pigeon's internal consumer dispatch queue is now enabled only when concurrency control or backpressure is explicitly configured.
- Updated Mule durable action dependencies to `1.4.1` for the core, in-memory outbox, and Entity Framework Core outbox providers.

------

## [v2.7.0] - 2026-08-18

### Added

- `OutboxSettings.ExecutionQueueCapacity` for configuring Mule's bounded execution queue from Pigeon outbox settings.
- `OutboxLaneSettings.ExecutionQueueCapacity` for lane-specific Mule execution queue capacity.

### Changed

- Updated Mule durable action dependencies to `1.4.0` for the core, in-memory outbox, and Entity Framework Core outbox providers.
- Pigeon outbox high-throughput defaults now configure Mule execution queue capacity.

------

## [v2.6.0] - 2026-08-17

### Added

- Async consume acceptance contract for broker adapters, allowing bounded consumer queues to apply async backpressure without sync-over-async calls in provider hot paths.
- Configurable RabbitMQ publisher channel pool through `RabbitSettings.PublisherChannelPoolSize`.
- Startup publish topology warmup through `GlobalSettingsBuilder.PreProvisionPublishRoutes`.

### Changed

- RabbitMQ publishing now uses pooled channels with one lock per channel instead of serializing all publishes through one shared channel.
- Azure Service Bus and Azure Event Grid now map `ConsumerExecution.MaxConcurrency` and `ConsumerExecution.PrefetchCount` to processor options.
- Event Hub processor disposal is async and no longer blocks on `DisposeAsync`.

------

## [v2.5.0] - 2026-08-16

### Added

- `ConsumerExecution.PrefetchCount` for broker prefetch control, with RabbitMQ prioritizing explicit prefetch over `MaxConcurrency`.
- `ConfigureHighThroughputConsumers` helper for opt-in bounded consumer concurrency, queue capacity, prefetch, and handler timeout defaults.
- Mule-backed outbox throughput settings for worker count, max degree of parallelism, drain limits, drain-until-empty behavior, yield between drain batches, and lane configuration.
- `OutboxSettings.ConfigureHighThroughput` helper for Mule-backed outbox providers.
- `IConsumerExecutionDiagnostics` for inspecting received messages, queued backlog, active handlers, acknowledgements, failures, queue wait time, and current consumer execution settings.
- Additional Mule runtime fields on `OutboxDiagnosticsSnapshot`, including throughput, lane backlog, runtime failures, and dispatch latency averages.

### Changed

- Updated Mule durable action dependencies to `1.3.0` for the core, in-memory outbox, and Entity Framework Core outbox providers.
- Pigeon outbox providers now consume Mule's high-throughput durable action runtime improvements.
- `Pigeon:ConsumerExecution` and `Pigeon:Outbox` configuration sections now bind into the global Pigeon settings before code-based configuration callbacks run.

------

## [v2.4.0] - 2026-08-14

### Changed

- Consumer dispatch no longer defaults to a CPU-core-based concurrency cap.
- RabbitMQ prefetch is only configured when `ConsumerExecution.MaxConcurrency` is explicitly set.
- `ConsumerExecution.MaxConcurrency` and `ConsumerExecution.QueueCapacity` are optional, allowing broker-driven dispatch by default and bounded dispatch only when configured.

------

## [v2.3.0] - 2026-08-10

### Added

- Mule-backed transactional outbox dispatch for persisted Pigeon publish intents.
- Durable outbox action bridge that dispatches stored Pigeon payloads through the existing producer pipeline.
- Mule-backed in-memory outbox provider for tests and samples.
- Mule-backed Entity Framework Core outbox provider for durable persistence, recovery, retry, cleanup, and diagnostics.

### Changed

- The transactional outbox now stores the final intercepted Pigeon payload as a Mule durable action before broker dispatch.
- Entity Framework Core outbox persistence now uses Mule durable action entities in the application `DbContext` model.
- Outbox diagnostics now read Mule durable action state while preserving Pigeon's `IOutboxDiagnostics` contract.

------

## [v2.2.0] - 2026-08-07

### Added

- `Pigeon.Testing` package for broker-free tests of producers, consumers, dispatch, headers, dead letters, and failure paths.
- In-memory testing transport registration through `AddPigeonTesting`.
- Assembly-based testing consumer registration through `AddPigeonTestingConsumers`.
- Adapter-friendly testing registration through `AddPigeonTestingAdapter`.
- Manual pending-message dispatch through `DispatchPendingAsync`.
- Published, consumed, dead-letter, and failure inspection through `IPigeonTestingTransport`.
- Testing assertions for published messages, consumed messages, dead-letter messages, consumer failures, metadata values, and simple equality.
- Failure simulation through `FailNext<TMessage>(Exception)`.

------

## [v2.1.0] - 2026-08-05

### Added

- Configurable direct publish behavior when an ambient transaction is active through `ConfigurePublishing`.
- `AmbientTransactionPublishBehavior.SuppressTransaction` for publishing directly to brokers outside the ambient transaction.
- `AmbientTransactionPublishBehavior.Throw` for failing fast when direct broker publishing is attempted inside an ambient transaction.

------

## [v2.0.0] - 2026-07-23

### Added

- Raw message publishing with `PublishRawAsync`, allowing producers to send payloads directly without the default `WrappedPayload` envelope.
- Routed publishing with `PublishingRoute`, including broker route metadata such as exchange, routing key, queue, subscription, and partition information.
- Broker-side fan-out support across adapters so one publish can be delivered to multiple configured consumers when the selected broker supports that behavior.
- Configurable topology provisioning with `Manual`, `OnStartup`, `OnPublish`, and `OnConsume` modes. Manual provisioning is the default.
- Cached topology provisioning records to avoid recreating queues, topics, subscriptions, exchanges, and bindings on every publish or consume.
- Configurable consumer acknowledgement behavior with manual ack, auto-ack on receive, and ack after a successful handler.
- Configurable consumer execution settings for concurrency, queue capacity, and handler timeout.
- `IConsumeContextAccessor` for resolving the current consume context from application services, returning `null` outside a Pigeon consume pipeline.
- Transactional outbox support in the producer pipeline, storing the final intercepted payload before broker dispatch.
- Immediate outbox dispatch through an in-memory queue, with interval-based recovery scans only for failed, retryable, or restarted messages.
- Outbox cleanup settings through `CleanInterval` and published message retention.
- Outbox diagnostics through `IOutboxDiagnostics` and `OutboxDiagnosticsSnapshot`.
- Entity Framework Core outbox provider in `Pigeon.Messaging.Outbox.EntityFrameworkCore`, including automatic model configuration and isolated outbox `DbContext` instances.
- In-memory broker adapter in `Pigeon.Messaging.InMemory` for unit tests, examples, and modular monolith scenarios.
- In-memory outbox provider in `Pigeon.Messaging.Outbox.InMemory` for tests and samples that need the real outbox pipeline without a database.
- Support for `net8.0`, `net9.0`, and `net10.0`.

### Changed

- Consumer dispatch now uses configurable background execution instead of blocking the broker receive loop.

### Removed

- Support for `net6.0` and `net7.0`.

------

## [v1.1.7] - 2026-01-05

### Fixed

- 🐛 Revert sanitization for Event Hub names.

------

## [v1.1.6] - 2026-01-05

### Fixed

- 🐛 Sanitize Event Hub names.

------

## [v1.1.5] - 2025-12-04

### Fixed

- 🐛 Fix endpoint research into routing topic for AzureEventGrid adapter. 

------

## [v1.1.4] - 2025-12-03

### Added

- 🎉 Add DefaultEndpoint support for AzureEventGrid configuration.

------

## [v1.1.3] - 2025-12-02

### Added

- 🎉 Add multiple TopicEndpoints configuration.

### Fixed

- 🐛 Sanitize event when EventGridConsuming adapter receives new message.

------

## [v1.1.2] - 2025-12-02

### Fixed

- 🐛 Add topic sanitization for EventGrid producing.

------

## [v1.1.1] - 2025-12-02

### Fixed

- 🐛 Use ServiceBus as consuming adapter for EventGrid.

------

## [v1.1.0] - 2025-12-01

### Added

- 🎉 Add support to Azure EventGrid and Azure EventHub adapters.

---

## [v1.0.11] - 2025-08-07

### Added

- 🎉 Add support to add and remove consumers in runtime.

------

## [v1.0.10] - 2025-08-06

### Fixed

- 🐛 Setup default JsonOptions.

------

## [v1.0.9] - 2025-08-06

### Added

- 🎉 Add JsonSerializer Options.

### Fixed

- 🐛 Remove full topic building.

------

## [v1.0.8] - 2025-07-23

### Added

- 🎉 New Azure Service Bus Adapter.

------

## [v1.0.6] - 2025-07-22

### Fixed

- 🐛 Sanitizes Topic when dispatch new message.
- 🐛 Corrects namespace for Kafka dependency injection extensions.
- 🐛 Returns only distinct topics when invoke GetAllTopics by IConsumeConfigurator.

------

## [v1.0.5] - 2025-07-22

### Fixed

- 🐛 Corrected full topic with Domain + Topic.

### Added

- 🎉 New Kafka Adapter.

------

## [v1.0.4] - 2025-07-06

### Fixed

- 🐛 Corrected payload serialization when producer publishes a message.

------

## [v1.0.3] - 2025-07-06

### Fixed

- 🐛 Corrected core consuming Dependency Injection.

------

## [v1.0.2] - 2025-07-06

### Fixed

- 🐛 Corrected RabbitMq Adapter Dependency Injection.

------

## [v1.0.1] - 2025-07-05

### Fixed

- 🐛 Corrected pack in publish pipeline.

------

## [v1.0.0] - 2025-07-05

### Added
- 🎉 First stable release of Pigeon.Messaging core.
- 🎉 Includes a RabbitMQ adapter in `Pigeon.Messaging.RabbitMq`.


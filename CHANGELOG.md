# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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


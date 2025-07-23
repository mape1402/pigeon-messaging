# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [v1.0.7] - 2025-07-23

- ### Added

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


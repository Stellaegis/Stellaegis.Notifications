# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [5.0.0] — 2026-10-21

The first version published on its own, extracted from the Stellaegis framework as a module of the family, on
Stellaegis.Primitives 5.0.0. The two packages move together.

### Added

- `Stellaegis.Notifications`: `INotificationChannel`, the port an application publishes through; `Notification`,
  addressed to one compartment, with its type, its payload, when it occurred and who sent it; `NotificationType`,
  `target.name` under the identifier rule of the family; the types an application declares, refused otherwise,
  with `NotificationTypeNotAllowedException` of code `NotificationType.NotAllowed`; `InMemoryNotificationChannel`,
  which delivers to the `INotificationSubscriber`s of the process; `NotificationsModule`, declared with
  compartments, compatible with an isolation by owner, with its operation `notifications.subscribe`.
- `Stellaegis.Notifications.SignalR`: `NotificationsHub`, mapped in the application's host under its own
  authentication by `MapNotificationsHub`, acting on spaces; `INotificationSubscriptionPolicy`, the application's
  rule that authorizes a subscription, required at registration; `SignalRNotificationChannel`, which pushes each
  notification to the connections of its recipient.

### Changed, from the framework's module

- `TenantNotification` becomes `Notification`: the recipient is a `Compartment`, the type a `NotificationType`
  written with underscores, and the sender an `Actor`.
- The server no longer registers a JWT bearer scheme of its own: the hub runs under the host's authentication,
  so that a single-process application maps it beside its API.
- The default resolver reading a `tenant` claim is gone: a policy of the application is required, and refusal is
  the default.
- The in-process channel delivers to subscribers instead of writing to a log.

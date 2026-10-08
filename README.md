# EventHorizon

[![CI](https://github.com/potatman/EventHorizon/actions/workflows/ci.yml/badge.svg)](https://github.com/potatman/EventHorizon/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

**EventHorizon** is a .NET framework for **Event Sourcing** and **Event Streaming**, providing a clean abstraction layer over multiple storage and streaming backends. Build event-driven applications with pluggable persistence (MongoDB, Elasticsearch, Apache Ignite, In-Memory) and messaging (Apache Pulsar, Kafka, In-Memory).

## Table of Contents

- [Features](#features)
- [Supported Platforms](#supported-platforms)
- [NuGet Packages](#nuget-packages)
- [Quick Start](#quick-start)
- [Core Concepts](#core-concepts)
- [Architecture](#architecture)
- [Storage Backends](#storage-backends)
- [Streaming Backends](#streaming-backends)
- [Configuration](#configuration)
- [Testing](#testing)
- [CI/CD](#cicd)
- [Samples](#samples)
- [Contributing](#contributing)
- [License](#license)

## Features

- **Event Sourcing** — Snapshot and View stores with automatic event application
- **Event Streaming** — Publish/subscribe with topic-based routing
- **CQRS** — Commands, Events, Requests, and Responses as first-class citizens
- **Pluggable Backends** — Swap storage and streaming providers without changing business logic
- **Aggregate Pattern** — Built-in aggregate lifecycle management with locking
- **Middleware** — Extensible pipeline for aggregate processing
- **Multi-Stream Subscriptions** — Subscribe to multiple event streams in a single consumer
- **Migration Support** — Built-in tooling for migrating between state schemas

## Supported Platforms

| .NET Version | Support Level | End of Support |
|---|---|---|
| .NET 10 | ✅ LTS | November 2028 |
| .NET 9 | ✅ STS | May 2026 |
| .NET 8 | ✅ LTS | November 2026 |

## NuGet Packages

All packages are published to [NuGet.org](https://www.nuget.org/).

| Package | Description |
|---|---|
| `EventHorizon.Abstractions` | Core interfaces, models, and attributes |
| `EventHorizon.EventStore` | Event store abstractions (CRUD stores, locks) |
| `EventHorizon.EventStore.InMemory` | In-memory event store (great for testing) |
| `EventHorizon.EventStore.MongoDb` | MongoDB-backed event store |
| `EventHorizon.EventStore.ElasticSearch` | Elasticsearch-backed event store |
| `EventHorizon.EventStore.Ignite` | Apache Ignite-backed event store |
| `EventHorizon.EventStreaming` | Event streaming abstractions |
| `EventHorizon.EventStreaming.InMemory` | In-memory streaming (great for testing) |
| `EventHorizon.EventStreaming.Pulsar` | Apache Pulsar streaming provider |
| `EventHorizon.EventSourcing` | Event sourcing orchestration (aggregates, senders, subscriptions) |

## Quick Start

### 1. Install packages

```bash
# Core + In-Memory (for getting started / testing)
dotnet add package EventHorizon.EventSourcing
dotnet add package EventHorizon.EventStore.InMemory
dotnet add package EventHorizon.EventStreaming.InMemory
```

### 2. Define your state

```csharp
using EventHorizon.Abstractions.Attributes;
using EventHorizon.Abstractions.Interfaces;
using EventHorizon.Abstractions.Interfaces.Actions;
using EventHorizon.Abstractions.Interfaces.Handlers;

[SnapshotStore("my_app_accounts")]
[Stream("$type")]
public class Account : IState,
    IHandleCommand<CreateAccount>,
    IApplyEvent<AccountCreated>
{
    public string Id { get; set; }
    public string Name { get; set; }
    public int Balance { get; set; }

    public void Handle(CreateAccount command, AggregateContext context)
    {
        context.AddEvent(new AccountCreated(command.Name, command.InitialBalance));
    }

    public void Apply(AccountCreated @event)
    {
        Name = @event.Name;
        Balance = @event.Balance;
    }
}
```

### 3. Define actions

```csharp
using EventHorizon.Abstractions.Interfaces.Actions;

public record CreateAccount(string Name, int InitialBalance) : ICommand<Account>;
public record AccountCreated(string Name, int Balance) : IEvent<Account>;
```

### 4. Register services

```csharp
using EventHorizon.Abstractions.Extensions;
using EventHorizon.EventSourcing.Extensions;
using EventHorizon.EventStore.InMemory.Extensions;
using EventHorizon.EventStreaming.InMemory.Extensions;

services.AddEventHorizon(x =>
{
    x.AddEventSourcing()
        .AddInMemorySnapshotStore()
        .AddInMemoryViewStore()
        .AddInMemoryEventStream()
        .ApplyCommandsToSnapshot<Account>();
});
```

### 5. Use the client

```csharp
using EventHorizon.EventSourcing;

public class AccountService
{
    private readonly EventSourcingClient<Account> _client;

    public AccountService(EventSourcingClient<Account> client)
    {
        _client = client;
    }

    public async Task CreateAccountAsync(string name, int balance)
    {
        await _client.CreateSender()
            .Send(new CreateAccount(name, balance))
            .ExecuteAsync();
    }
}
```

## Core Concepts

### State (`IState`)

The root entity that represents the current state of your domain object. Must implement `IState` with an `Id` property.

### Actions

EventHorizon uses a CQRS-style action hierarchy:

| Action | Interface | Purpose |
|---|---|---|
| **Command** | `ICommand<T>` | Mutates state — handled by `IHandleCommand<T>` on the state class |
| **Event** | `IEvent<T>` | Records what happened — applied by `IApplyEvent<T>` on the state class |
| **Request** | `IRequest<T, TResponse>` | Query or operation that returns a response |
| **Response** | `IResponse<T>` | Result of a request |

### Snapshots and Views

- **Snapshot** (`Snapshot<T>`) — The authoritative persisted state, rebuilt by replaying events
- **View** (`View<T>`) — A read-optimized projection derived from events, can combine data from multiple streams

### Aggregates

The `AggregateBuilder` manages the lifecycle of loading state from a store, applying actions, and persisting results. It handles optimistic concurrency via sequence IDs and distributed locking.

### Subscriptions

`SubscriptionBuilder<T>` creates durable consumers that process messages from one or more streams. Implement `IStreamConsumer<T>` to handle batches of messages:

```csharp
public class MyConsumer : IStreamConsumer<Event>
{
    public Task OnBatch(SubscriptionContext<Event> context)
    {
        foreach (var message in context.Messages)
        {
            var payload = message.Data.GetPayload();
            // Process event...
        }
        return Task.CompletedTask;
    }
}
```

Register with:

```csharp
x.AddSubscription<MyConsumer, Event>(s => s.AddStream<Account>());
```

### Middleware

Aggregate processing supports middleware for cross-cutting concerns:

```csharp
x.ApplyEventsToView<MyView>(h => h.UseMiddleware<MyMiddleware>());
```

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Your Application                         │
│  ┌──────────────────┐  ┌──────────────────┐                │
│  │ EventSourcingClient│  │  StreamingClient  │               │
│  └────────┬─────────┘  └────────┬─────────┘                │
├───────────┼──────────────────────┼──────────────────────────┤
│           │   EventHorizon Core  │                          │
│  ┌────────▼─────────┐  ┌────────▼─────────┐                │
│  │  AggregateBuilder │  │ SubscriptionBuilder│               │
│  │  SenderBuilder    │  │ PublisherBuilder   │               │
│  │  ICrudStore<T>    │  │ ReaderBuilder      │               │
│  └────────┬─────────┘  └────────┬─────────┘                │
├───────────┼──────────────────────┼──────────────────────────┤
│  ┌────────▼─────────┐  ┌────────▼─────────┐                │
│  │   Event Stores    │  │ Event Streaming   │                │
│  │  ┌─────────────┐  │  │ ┌─────────────┐  │                │
│  │  │  MongoDB     │  │  │ │  Pulsar     │  │                │
│  │  │  Elastic     │  │  │ │  Kafka      │  │                │
│  │  │  Ignite      │  │  │ │  In-Memory  │  │                │
│  │  │  In-Memory   │  │  │ └─────────────┘  │                │
│  │  └─────────────┘  │  └──────────────────┘                │
│  └──────────────────┘                                       │
└─────────────────────────────────────────────────────────────┘
```

## Storage Backends

### MongoDB

```csharp
x.AddMongoDbSnapshotStore(config.GetSection("MongoDb").Bind)
 .AddMongoDbViewStore(config.GetSection("MongoDb").Bind);
```

```json
{
  "MongoDb": {
    "ConnectionString": "mongodb://localhost:27017",
    "Database": "my_database"
  }
}
```

### Elasticsearch

```csharp
x.AddElasticSnapshotStore(config.GetSection("ElasticSearch").Bind)
 .AddElasticViewStore(config.GetSection("ElasticSearch").Bind);
```

```json
{
  "ElasticSearch": {
    "Uri": "http://localhost:9200"
  }
}
```

### Apache Ignite

```csharp
x.AddIgniteSnapshotStore(config.GetSection("Ignite").Bind)
 .AddIgniteViewStore(config.GetSection("Ignite").Bind);
```

### In-Memory

```csharp
x.AddInMemorySnapshotStore()
 .AddInMemoryViewStore();
```

Best suited for unit/integration testing. No external dependencies required.

## Streaming Backends

### Apache Pulsar

```csharp
x.AddPulsarEventStream(config.GetSection("Pulsar").Bind);
```

```json
{
  "Pulsar": {
    "ServiceUrl": "pulsar://localhost:6650"
  }
}
```

### In-Memory

```csharp
x.AddInMemoryEventStream();
```

Best suited for unit/integration testing. No external dependencies required.

## Configuration

### Attributes

| Attribute | Target | Purpose |
|---|---|---|
| `[SnapshotStore("bucket_id")]` | Class | Configures the snapshot store bucket/collection name |
| `[ViewStore("database")]` | Class | Configures the view store database/index name |
| `[Stream("topic")]` | Class | Maps a type to a streaming topic |
| `[StreamPartitionKey]` | Property | Designates the property used for stream partitioning |

### Docker Compose

Development infrastructure is provided in the `compose/` directory:

```bash
# Start MongoDB
docker compose -f compose/MongoDb/docker-compose.yml up -d

# Start Elasticsearch
docker compose -f compose/ElasticSearch/docker-compose.yml up -d

# Start Pulsar
docker compose -f compose/Pulsar/docker-compose.yml up -d

# Start Ignite
docker compose -f compose/Ignite/docker-compose.yml up -d
```

## Testing

The test suite uses **xUnit** with **Bogus** for data generation.

```bash
# Run unit tests only
dotnet test --filter "Category!=Integration"

# Run all tests (requires Docker services)
dotnet test
```

### Writing Tests

Use the in-memory providers for fast, isolated unit tests:

```csharp
services.AddEventHorizon(x =>
{
    x.AddInMemorySnapshotStore()
     .AddInMemoryViewStore()
     .AddInMemoryEventStream()
     .AddEventSourcing();
});
```

Integration tests use `[Collection("Integration")]` and require running Docker Compose services.

## CI/CD

This project uses **GitHub Actions** (`.github/workflows/ci.yml`) with **[GitVersion](https://gitversion.net/)** for semantic versioning. Development is **trunk-based**: `master` is the only long-lived branch, and short-lived `feature/*` branches merge into it through pull requests.

| Branch/Tag | Published | Example Version |
|---|---|---|
| SemVer tag (`1.6.0`) on `master` | stable | `1.6.0` |
| `master` (untagged) | pre-release | `1.7.0-preview.3` |
| `feature/*`, pull requests | build and test only | `1.7.0-my-feature.1` |

### Releasing

- **Stable releases come only from tags.** Tag the commit on `master` and push the tag (`git tag 1.6.0 && git push origin 1.6.0`). CI rejects tags that are not plain `X.Y.Z` (an optional `v` prefix is accepted) or whose commit is not on `master`.
- Every untagged `master` commit publishes `{next}-preview.{n}`, where `{next}` is the last tag with its minor version bumped. Commit messages with `+semver: major` or `+semver: patch` change the bump.
- Configuration lives in `GitVersion.yml` at the repo root.

### Trusted Publishing

NuGet packages are published using [trusted publishing](https://devblogs.microsoft.com/nuget/introducing-trusted-publishers/) via GitHub's OIDC tokens — no API keys or secrets required. The trusted publisher is configured on nuget.org to trust the `ci.yml` workflow in this repository.

## Samples

Working examples are in the `samples/` directory:

- **`EventHorizon.EventSourcing.Samples`** — Full event sourcing example with accounts, commands, events, views, and subscriptions using MongoDB + Elasticsearch + Pulsar
- **`EventHorizon.EventStreaming.Samples`** — Standalone streaming example with multi-topic subscription and publishing

Run samples with:

```bash
# Start required infrastructure
docker compose -f compose/MongoDb/docker-compose.yml up -d
docker compose -f compose/ElasticSearch/docker-compose.yml up -d
docker compose -f compose/Pulsar/docker-compose.yml up -d

# Run the event sourcing sample
dotnet run --project samples/EventHorizon.EventSourcing.Samples
```

## Project Structure

```
EventHorizon/
├── src/
│   ├── EventHorizon.Abstractions/          # Core interfaces, models, attributes
│   ├── EventHorizon.EventStore/            # Store abstractions (ICrudStore, Lock)
│   ├── EventHorizon.EventStore.InMemory/   # In-memory store implementation
│   ├── EventHorizon.EventStore.MongoDb/    # MongoDB store implementation
│   ├── EventHorizon.EventStore.ElasticSearch/ # Elasticsearch store implementation
│   ├── EventHorizon.EventStore.Ignite/     # Apache Ignite store implementation
│   ├── EventHorizon.EventStreaming/        # Streaming abstractions
│   ├── EventHorizon.EventStreaming.InMemory/ # In-memory streaming
│   ├── EventHorizon.EventStreaming.Pulsar/ # Apache Pulsar streaming
│   └── EventHorizon.EventSourcing/        # Event sourcing orchestration
├── test/                                   # Unit and integration tests
├── samples/                                # Working example applications
├── benchmark/                              # Performance benchmarks
├── compose/                                # Docker Compose files for local dev
└── charts/                                 # Helm charts for Kubernetes deployment
```

## Contributing

1. Fork the repository
2. Create a feature branch (`feature/my-feature`)
3. Commit changes with clear messages
4. Open a pull request against `master`

## License

This project is licensed under the [MIT License](LICENSE).
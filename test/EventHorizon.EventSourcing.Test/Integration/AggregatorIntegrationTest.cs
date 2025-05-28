﻿using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Destructurama;
using EventHorizon.Abstractions.Extensions;
using EventHorizon.Abstractions.Models.TopicMessages;
using EventHorizon.Abstractions.Testing;
using EventHorizon.EventSourcing.Aggregates;
using EventHorizon.EventSourcing.Extensions;
using EventHorizon.EventSourcing.Samples.Models.Actions;
using EventHorizon.EventSourcing.Samples.Models.Snapshots;
using EventHorizon.EventSourcing.Samples.Models.View;
using EventHorizon.EventSourcing.Test.Fakers;
using EventHorizon.EventStore.Extensions;
using EventHorizon.EventStore.InMemory.Extensions;
using EventHorizon.EventStore.Interfaces.Factory;
using EventHorizon.EventStore.Interfaces.Stores;
using EventHorizon.EventStore.Models;
using EventHorizon.EventStreaming;
using EventHorizon.EventStreaming.InMemory.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Xunit;
using Xunit.Abstractions;

namespace EventHorizon.EventSourcing.Test.Integration;

[Trait("Category", "Integration")]
public class AggregatorIntegrationTest : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly IHost _host;
    private readonly StreamingClient _streamingClient;
    private Stopwatch _stopwatch;
    private readonly ICrudStore<Snapshot<Account>> _snapshotStore;
    private readonly Aggregator<Snapshot<Account>, Account> _accountAggregator;
    private readonly Aggregator<Snapshot<User>, User> _userAggregator;
    private readonly EventSourcingClient<Account> _eventSourcingClient;

    public AggregatorIntegrationTest(ITestOutputHelper output)
    {
        _output = output;
        _host = Host.CreateDefaultBuilder(Array.Empty<string>())
            .ConfigureServices((hostContext, services) =>
            {
                services.AddEventHorizon(x =>
                {
                    x.AddEventSourcing()

                        // Hosts
                        .ApplyRequestsToSnapshot<Account>()
                        .ApplyCommandsToSnapshot<User>()
                        .ApplyRequestsToSnapshot<SearchAccountView>()

                        // Stores
                        .AddInMemorySnapshotStore()
                        .AddInMemoryViewStore()
                        .AddInMemoryEventStream();
                });
            })
            .UseSerilog((_, config) =>
            {
                config.WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
                    .WriteTo.TestOutput(output, LogEventLevel.Information, formatProvider: CultureInfo.InvariantCulture)
                    .Destructure.UsingAttributes();
            })
            .UseEnvironment("test")
            .Build()
            .AddTestBucketIds();

        _eventSourcingClient = _host.Services.GetRequiredService<EventSourcingClient<Account>>();
        _accountAggregator = _eventSourcingClient.Aggregator().Build();
        _userAggregator = _host.Services.GetRequiredService<EventSourcingClient<User>>().Aggregator().Build();


        _streamingClient = _host.Services.GetRequiredService<StreamingClient>();
        _snapshotStore = _host.Services.GetRequiredService<ISnapshotStoreFactory<Account>>().GetSnapshotStore();
    }

    public async Task InitializeAsync()
    {
        await _host.StartAsync();
        _stopwatch = Stopwatch.StartNew();
    }

    public async Task DisposeAsync()
    {
        _output.WriteLine($"Test Ran in {_stopwatch.ElapsedMilliseconds}ms");
        await _snapshotStore.DropDatabaseAsync(CancellationToken.None);
        await _streamingClient.GetAdmin<Event>().DeleteTopicAsync(typeof(Account));
        await _host.StopAsync();
        _host.Dispose();
    }

    [Fact]
    public async Task TestRebuild()
    {
        var streamId = EventSourcingFakers.Faker.Random.AlphaNumeric(9);
        var publisher = _streamingClient.CreatePublisher<Event>().AddStream<Account>().Build();

        // Setup Event
        await publisher.PublishAsync(streamId, new AccountOpened(100));

        // Refresh Snapshots
        await _eventSourcingClient.Aggregator().Build().RebuildAllAsync(CancellationToken.None);

        // Assert
        var aggregate  = await _eventSourcingClient.GetSnapshotStore().GetAsync(streamId, CancellationToken.None);
        Assert.Equal(streamId, aggregate.State.Id);
        Assert.Equal(streamId, aggregate.Id);
        Assert.NotEqual(DateTime.MinValue, aggregate.CreatedDate);
        Assert.NotEqual(DateTime.MinValue, aggregate.UpdatedDate);
        Assert.Equal(100, aggregate.State.Amount);
    }

    [Fact]
    public async Task TestCommands()
    {
        // Setup
        var streamId = EventSourcingFakers.Faker.Random.AlphaNumeric(9);
        var command1 = new Command(streamId, new ChangeUserName("Bob"));
        var command2 = new Command(streamId, new ChangeUserName("Joe"));

        // Act
        var res1 = await _userAggregator.HandleAsync(command1, CancellationToken.None);
        var res2 = await _userAggregator.HandleAsync(command2, CancellationToken.None);

        // Assert Account
        var aggregate1  = await _userAggregator.GetAggregateFromStateAsync(streamId, CancellationToken.None);
        Assert.Equal(streamId, aggregate1.State.Id);
        Assert.Equal(streamId, aggregate1.Id);
        Assert.Equal(2, aggregate1.SequenceId);
        Assert.NotEqual(DateTime.MinValue, aggregate1.CreatedDate);
        Assert.NotEqual(DateTime.MinValue, aggregate1.UpdatedDate);
        Assert.Equal("Joe", aggregate1.State.Name);
    }

    [Fact]
    public async Task TestEvents()
    {
        // Setup
        var streamId = EventSourcingFakers.Faker.Random.AlphaNumeric(9);
        var @event = new Event(streamId, 1, new AccountOpened(100));

        // Act
        var res = await _accountAggregator.HandleAsync(@event, CancellationToken.None);

        // Assert Account
        var aggregate1  = await _accountAggregator.GetAggregateFromStateAsync(streamId, CancellationToken.None);
        Assert.Equal(streamId, aggregate1.State.Id);
        Assert.Equal(streamId, aggregate1.Id);
        Assert.Equal(1, aggregate1.SequenceId);
        Assert.NotEqual(DateTime.MinValue, aggregate1.CreatedDate);
        Assert.NotEqual(DateTime.MinValue, aggregate1.UpdatedDate);
        Assert.Equal(100, aggregate1.State.Amount);
    }
}

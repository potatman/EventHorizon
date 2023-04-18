using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Attributes;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.Abstractions.Testing;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventSourcing.Aggregates;
using Insperex.EventHorizon.EventSourcing.Extensions;
using Insperex.EventHorizon.EventSourcing.Samples.Models.Snapshots;
using Insperex.EventHorizon.EventSourcing.Samples.Models.View;
using Insperex.EventHorizon.EventSourcing.Test.Fakers;
using Insperex.EventHorizon.EventStore.InMemory.Extensions;
using Insperex.EventHorizon.EventStore.Interfaces.Factory;
using Insperex.EventHorizon.EventStore.Interfaces.Stores;
using Insperex.EventHorizon.EventStore.Models;
using Insperex.EventHorizon.EventStreaming;
using Insperex.EventHorizon.EventStreaming.InMemory.Extensions;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Xunit;
using Xunit.Abstractions;

namespace Insperex.EventHorizon.EventSourcing.Test.Integration;

[Trait("Category", "Integration")]
public class AggregatorIntegrationTest : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly IHost _host;
    private readonly StreamingClient _streamingClient;
    private Stopwatch _stopwatch;
    private readonly IStreamFactory _streamFactory;
    private readonly ICrudStore<Snapshot<Account>> _snapshotStore;
    private readonly AggregatorManager<Snapshot<Account>, Account> _accountAggregatorManager;
    private readonly Aggregator<Snapshot<Account>, Account> _accountAggregator;
    private readonly AggregatorManager<Snapshot<User>, User> _userAggregateManager;
    private readonly Aggregator<Snapshot<User>, User> _userAggregator;

    public AggregatorIntegrationTest(ITestOutputHelper output)
    {
        _output = output;
        _host = Host.CreateDefaultBuilder(new string[] { })
            .ConfigureServices((hostContext, services) =>
            {
                services.AddInMemorySnapshotStore();
                services.AddInMemoryEventStream();
                // services.Stream(hostContext.Configuration);
                // services.AddMongoDbEventStore(hostContext.Configuration);
                
                services.AddEventSourcing();
                services.AddHostedAggregate<Account>();
                services.AddHostedAggregate<User>();
                services.AddHostedAggregate<SearchAccountView>();
            })
            .UseSerilog((_, config) =>
            {
                config.WriteTo.Console()
                    .WriteTo.TestOutput(output, LogEventLevel.Information);
            })
            .UseEnvironment("test")
            .Build()
            .AddTestBucketIds();

        _accountAggregator = _host.Services.GetRequiredService<Aggregator<Snapshot<Account>, Account>>();
        _accountAggregatorManager = _host.Services.GetRequiredService<AggregatorManager<Snapshot<Account>, Account>>();
        _userAggregator = _host.Services.GetRequiredService<Aggregator<Snapshot<User>, User>>();
        _userAggregateManager = _host.Services.GetRequiredService<AggregatorManager<Snapshot<User>, User>>();

        
        _streamingClient = _host.Services.GetRequiredService<StreamingClient>();
        _streamFactory = _host.Services.GetRequiredService<IStreamFactory>();
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
        foreach (var topic in _streamFactory.GetTopicResolver().GetTopics<Event>(typeof(Account)))
            await _streamFactory.CreateAdmin().DeleteTopicAsync(topic, CancellationToken.None);
        await _host.StopAsync();
        _host.Dispose();
    }

    [Fact]
    public async Task TestRebuild()
    {
        var streamId = EventSourcingFakers.Faker.Random.AlphaNumeric(9);
        var publisher = _streamingClient.CreatePublisher<Event>().AddTopic<Account>().Build();

        // Setup Event
        await publisher.PublishAsync(new Event(streamId, new AccountOpened(100)));

        // Refresh Snapshots
        await _accountAggregator.RebuildAllAsync(CancellationToken.None);
        
        // Assert
        var aggregate  = await _accountAggregator.GetAsync(streamId);
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
        var command = new Command(streamId, new CreateUser("Bob"));

        // Act
        await _userAggregateManager.Handle(new [] {command}, 0, CancellationToken.None);

        // Assert Account
        var aggregate1  = await _userAggregator.GetAsync(streamId);
        Assert.Equal(streamId, aggregate1.State.Id);
        Assert.Equal(streamId, aggregate1.Id);
        Assert.NotEqual(DateTime.MinValue, aggregate1.CreatedDate);
        Assert.NotEqual(DateTime.MinValue, aggregate1.UpdatedDate);
        Assert.Equal("Bob", aggregate1.State.Name);
    }
    

    [Fact]
    public async Task TestEvents()
    {
        // Setup
        var streamId = EventSourcingFakers.Faker.Random.AlphaNumeric(9);
        var @event = new Event(streamId, new AccountOpened(100));

        // Act
        await _accountAggregatorManager.Handle(new [] {@event}, 0, CancellationToken.None);

        // Assert Account
        var aggregate1  = await _accountAggregator.GetAsync(streamId);
        Assert.Equal(streamId, aggregate1.State.Id);
        Assert.Equal(streamId, aggregate1.Id);
        Assert.NotEqual(DateTime.MinValue, aggregate1.CreatedDate);
        Assert.NotEqual(DateTime.MinValue, aggregate1.UpdatedDate);
        Assert.Equal(100, aggregate1.State.Amount);
    }
}
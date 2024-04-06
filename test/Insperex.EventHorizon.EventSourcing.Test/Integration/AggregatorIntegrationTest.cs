using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Destructurama;
using Insperex.EventHorizon.Abstractions.Extensions;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.Abstractions.Serialization.Compression;
using Insperex.EventHorizon.Abstractions.Testing;
using Insperex.EventHorizon.EventSourcing.Aggregates;
using Insperex.EventHorizon.EventSourcing.AggregateWorkflows.Workflows;
using Insperex.EventHorizon.EventSourcing.Extensions;
using Insperex.EventHorizon.EventSourcing.Samples.Models.Actions;
using Insperex.EventHorizon.EventSourcing.Samples.Models.Snapshots;
using Insperex.EventHorizon.EventSourcing.Samples.Models.View;
using Insperex.EventHorizon.EventSourcing.Test.Fakers;
using Insperex.EventHorizon.EventStore;
using Insperex.EventHorizon.EventStore.Extensions;
using Insperex.EventHorizon.EventStore.InMemory.Extensions;
using Insperex.EventHorizon.EventStore.Interfaces.Stores;
using Insperex.EventHorizon.EventStore.Models;
using Insperex.EventHorizon.EventStreaming;
using Insperex.EventHorizon.EventStreaming.InMemory.Extensions;
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
    private readonly Aggregator<Snapshot<User>, User> _userAggregator;
    private readonly EventSourcingClient<Account> _eventSourcingClient;
    private readonly HandleAndApplyEventsWorkflow<Snapshot<User>, User, Command> _userCommandWorkflow;
    private readonly ApplyEventsWorkflow<View<Account>, Account> _accountApplyEventWorkflow;
    private readonly Store<View<Account>, Account> _accountViewStore;

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
                        // .HandleRequests<Account>()
                        // .HandleCommands<User>()
                        // .ApplyEvents<SearchAccountView>()

                        // Stores
                        .AddInMemorySnapshotStore()
                        .AddInMemoryViewStore()
                        .AddInMemoryEventStream()

                        ;
                });
                services.AddTestingForEventHorizon();
            })
            .UseSerilog((_, config) =>
            {
                config.WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
                    .WriteTo.TestOutput(output, LogEventLevel.Information, formatProvider: CultureInfo.InvariantCulture)
                    .Destructure.UsingAttributes();
            })
            .UseEnvironment("test")
            .Build();

        _eventSourcingClient = _host.Services.GetRequiredService<EventSourcingClient<Account>>();
        _accountViewStore = _eventSourcingClient.GetViewStore().Build();
        _userAggregator = _host.Services.GetRequiredService<EventSourcingClient<User>>().Aggregator()
            .Build();

        _userCommandWorkflow = _host.Services.GetRequiredService<EventSourcingClient<User>>().Workflow()
            .HandleCommands(x =>
                x.WithAggregate(b => b.StateCompression(Compression.Gzip).EventCompression(Compression.Gzip))
            );
        _accountApplyEventWorkflow = _host.Services.GetRequiredService<EventSourcingClient<Account>>().Workflow()
            .ApplyEvents(x =>
                x.WithAggregate(b => b.StateCompression(Compression.Gzip).EventCompression(Compression.Gzip))
            );


        _streamingClient = _host.Services.GetRequiredService<StreamingClient>();
    }

    public async Task InitializeAsync()
    {
        await _host.StartAsync();
        _stopwatch = Stopwatch.StartNew();
    }

    public async Task DisposeAsync()
    {
        _output.WriteLine($"Test Ran in {_stopwatch.ElapsedMilliseconds}ms");
        await _accountViewStore.DropDatabaseAsync(CancellationToken.None);
        await _userAggregator.DropAllAsync(CancellationToken.None);
        await _host.StopAsync();
        _host.Dispose();
    }

    [Fact]
    public async Task TestRebuild()
    {
        var streamId = EventSourcingFakers.Faker.Random.AlphaNumeric(9);
        var publisher = _streamingClient.CreatePublisher<Event>().AddStateStream<Account>().Build();

        // Setup Event
        await publisher.PublishAsync(streamId, new AccountOpened(100));

        // Refresh Snapshots
        await _eventSourcingClient.Workflow().RebuildAll().StartAsync(CancellationToken.None);
        await Task.Delay(2000);

        // Assert
        var aggregate  = await _eventSourcingClient.Aggregator().Build().GetAggregateFromStateAsync(streamId, CancellationToken.None);
        Assert.Equal(streamId, aggregate.Payload.Id);
        Assert.Equal(streamId, aggregate.Id);
        Assert.NotEqual(DateTime.MinValue, aggregate.CreatedDate);
        Assert.NotEqual(DateTime.MinValue, aggregate.UpdatedDate);
        Assert.Equal(100, aggregate.Payload.Amount);
    }

    [Fact]
    public async Task TestCommands()
    {
        // Setup
        var streamId = EventSourcingFakers.Faker.Random.AlphaNumeric(9);
        var command1 = new Command(streamId, new ChangeUserName("Bob"));
        var command2 = new Command(streamId, new ChangeUserName("Joe"));

        // Act
        await _userCommandWorkflow.Handle(command1, CancellationToken.None);
        await _userCommandWorkflow.Handle(command2, CancellationToken.None);

        // Assert Account
        var aggregate1  = await _userAggregator.GetAggregateFromStateAsync(streamId, CancellationToken.None);
        Assert.Equal(streamId, aggregate1.Payload.Id);
        Assert.Equal(streamId, aggregate1.Id);
        Assert.Equal(2, aggregate1.SequenceId);
        Assert.NotEqual(DateTime.MinValue, aggregate1.CreatedDate);
        Assert.NotEqual(DateTime.MinValue, aggregate1.UpdatedDate);
        Assert.Equal("Joe", aggregate1.Payload.Name);
    }

    [Fact]
    public async Task TestEvents()
    {
        // Setup
        var streamId = EventSourcingFakers.Faker.Random.AlphaNumeric(9);
        var @event = new Event(streamId, 1, new AccountOpened(100));

        // Act
        await _accountApplyEventWorkflow.Handle(@event, CancellationToken.None);

        // Assert Account
        var aggregate1  = await _accountViewStore.GetAsync(streamId, CancellationToken.None);
        Assert.Equal(streamId, aggregate1.Payload.Id);
        Assert.Equal(streamId, aggregate1.Id);
        Assert.Equal(1, aggregate1.SequenceId);
        Assert.NotEqual(DateTime.MinValue, aggregate1.CreatedDate);
        Assert.NotEqual(DateTime.MinValue, aggregate1.UpdatedDate);
        Assert.Equal(100, aggregate1.Payload.Amount);
    }
}

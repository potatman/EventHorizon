using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Destructurama;
using Insperex.EventHorizon.Abstractions.Extensions;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.Abstractions.Testing;
using Insperex.EventHorizon.EventSourcing.Aggregates;
using Insperex.EventHorizon.EventSourcing.Extensions;
using Insperex.EventHorizon.EventSourcing.Samples.Models.Actions;
using Insperex.EventHorizon.EventSourcing.Samples.Models.Snapshots;
using Insperex.EventHorizon.EventSourcing.Samples.Models.View;
using Insperex.EventHorizon.EventStore.InMemory.Extensions;
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
public class ViewIndexerIntegrationTest : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly IHost _host;
    private readonly StreamingClient _streamingClient;
    private Stopwatch _stopwatch;
    private readonly Aggregator<View<AccountView>, AccountView> _accountAggregate;
    private readonly Aggregator<View<SearchAccountView>, SearchAccountView> _userAccountStore;

    public ViewIndexerIntegrationTest(ITestOutputHelper output)
    {
        _output = output;
        _host = Host.CreateDefaultBuilder(Array.Empty<string>())
            .ConfigureServices((hostContext, services) =>
            {
                services.AddEventHorizon(x =>
                {
                    x.AddEventSourcing()

                        // Hosts
                        .ApplyEvents<AccountView>()
                        .ApplyEvents<SearchAccountView>()

                        // Stores
                        .AddInMemorySnapshotStore()
                        .AddInMemoryViewStore()
                        .AddInMemoryEventStream();
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

        _streamingClient = _host.Services.GetRequiredService<StreamingClient>();
        _accountAggregate = _host.Services.GetRequiredService<EventSourcingClient<AccountView>>().ViewAggregator().Build();
        _userAccountStore = _host.Services.GetRequiredService<EventSourcingClient<SearchAccountView>>().ViewAggregator().Build();
    }

    public async Task InitializeAsync()
    {
        await _host.StartAsync();
        _stopwatch = Stopwatch.StartNew();
    }

    public async Task DisposeAsync()
    {
        _output.WriteLine($"Test Ran in {_stopwatch.ElapsedMilliseconds}ms");
        await _host.StopAsync();
        await _accountAggregate.DropAllAsync(CancellationToken.None);
        await _userAccountStore.DropAllAsync(CancellationToken.None);
        _host.Dispose();
    }

    [Fact]
    public async Task TestViewIndexer()
    {
        var streamId = "123";
        var publisher = _streamingClient.CreatePublisher<Event>().AddStateStream<Account>().Build();

        // Setup Event
        await publisher.PublishAsync(new Event(streamId, new AccountOpened(100)));

        // Wait for Subscription
        await Task.Delay(TimeSpan.FromSeconds(3));

        // Assert Account
        var view = await _accountAggregate.GetAggregateFromStateAsync(streamId, CancellationToken.None);
        Assert.Equal(streamId, view.Payload.Id);
        Assert.Equal(streamId, view.Id);
        Assert.NotEqual(DateTime.MinValue, view.CreatedDate);
        Assert.NotEqual(DateTime.MinValue, view.UpdatedDate);
        Assert.Equal(100, view.Payload.Amount);

        // Assert UserAccount
        var view2 = await _userAccountStore.GetAggregateFromStateAsync(streamId, CancellationToken.None);
        Assert.Equal(streamId, view2.Payload.Id);
        Assert.Equal(streamId, view2.Id);
        Assert.NotEqual(DateTime.MinValue, view2.CreatedDate);
        Assert.NotEqual(DateTime.MinValue, view2.UpdatedDate);
        Assert.Equal(100, view2.Payload.Account.Amount);
    }
}

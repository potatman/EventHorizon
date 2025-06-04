﻿using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Destructurama;
using EventHorizon.Abstractions.Extensions;
using EventHorizon.Abstractions.Models.TopicMessages;
using EventHorizon.Abstractions.Testing;
using EventHorizon.EventSourcing.Extensions;
using EventHorizon.EventSourcing.Samples.Models.Actions;
using EventHorizon.EventSourcing.Samples.Models.Snapshots;
using EventHorizon.EventSourcing.Samples.Models.View;
using EventHorizon.EventStore.ElasticSearch.Extensions;
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
public class ViewIndexerIntegrationTest : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly IHost _host;
    private readonly StreamingClient _streamingClient;
    private Stopwatch _stopwatch;
    private readonly ICrudStore<View<AccountView>> _accountStore;
    private readonly ICrudStore<View<SearchAccountView>> _userAccountStore;

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
                        .ApplyEventsToView<AccountView>()
                        .ApplyEventsToView<SearchAccountView>()

                        // Stores
                        .AddInMemorySnapshotStore()
                        .AddInMemoryViewStore()
                        // .AddElasticViewStore()
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

        _streamingClient = _host.Services.GetRequiredService<StreamingClient>();
        _accountStore = _host.Services.GetRequiredService<IViewStoreFactory<AccountView>>().GetViewStore();
        _userAccountStore = _host.Services.GetRequiredService<IViewStoreFactory<SearchAccountView>>().GetViewStore();
    }

    public async Task InitializeAsync()
    {
        await _host.StartAsync();
        _stopwatch = Stopwatch.StartNew();
    }

    public async Task DisposeAsync()
    {
        _output.WriteLine($"Test Ran in {_stopwatch.ElapsedMilliseconds}ms");
        await _accountStore.DropDatabaseAsync(CancellationToken.None);
        await _userAccountStore.DropDatabaseAsync(CancellationToken.None);

        // Delete Event Dbs
        await _streamingClient.GetAdmin<Event>().DeleteTopicAsync(typeof(Account));
        await _streamingClient.GetAdmin<Event>().DeleteTopicAsync(typeof(User));

        await _host.StopAsync();
        _host.Dispose();
    }

    [Fact]
    public async Task TestViewIndexer()
    {
        var streamId = "123";
        var publisher = _streamingClient.CreatePublisher<Event>().AddStream<Account>().Build();

        // Setup Event
        await publisher.PublishAsync(new Event(streamId, new AccountOpened(100)));

        // Wait for Subscription
        await Task.Delay(TimeSpan.FromSeconds(3));

        // Assert Account
        var views = await _accountStore.GetAllAsync(new [] { streamId }, CancellationToken.None);
        var view = views.FirstOrDefault();
        Assert.Equal(streamId, view.State.Id);
        Assert.Equal(streamId, view.Id);
        Assert.NotEqual(DateTime.MinValue, view.CreatedDate);
        Assert.NotEqual(DateTime.MinValue, view.UpdatedDate);
        Assert.Equal(100, view.State.Amount);

        // Assert UserAccount
        var views2 = await _userAccountStore.GetAllAsync(new [] { streamId }, CancellationToken.None);
        var view2 = views2.FirstOrDefault();
        Assert.Equal(streamId, view2.State.Id);
        Assert.Equal(streamId, view2.Id);
        Assert.NotEqual(DateTime.MinValue, view2.CreatedDate);
        Assert.NotEqual(DateTime.MinValue, view2.UpdatedDate);
        Assert.Equal(100, view2.State.Account.Amount);
    }
}

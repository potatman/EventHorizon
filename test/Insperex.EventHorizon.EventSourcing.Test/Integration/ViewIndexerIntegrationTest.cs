using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.Abstractions.Testing;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventSourcing.Extensions;
using Insperex.EventHorizon.EventSourcing.Samples.Models.Snapshots;
using Insperex.EventHorizon.EventSourcing.Samples.Models.View;
using Insperex.EventHorizon.EventStore.ElasticSearch.Extensions;
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
public class ViewIndexerIntegrationTest : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly IHost _host;
    private readonly StreamingClient _streamingClient;
    private Stopwatch _stopwatch;
    private readonly IStreamFactory _streamFactory;
    private readonly ICrudStore<View<AccountView>> _accountStore;
    private readonly ICrudStore<View<SearchAccountView>> _userAccountStore;

    public ViewIndexerIntegrationTest(ITestOutputHelper output)
    {
        _output = output;
        _host = Host.CreateDefaultBuilder(new string[] { })
            .ConfigureServices((hostContext, services) =>
            {
                services.AddInMemorySnapshotStore();
                services.AddInMemoryEventStream();
                // services.AddInMemoryViewStore();
                // services.AddMongoDbSnapshotStore(hostContext.Configuration);
                // services.AddPulsarEventStream(hostContext.Configuration);
                services.AddElasticViewStore(hostContext.Configuration);
                
                services.AddEventSourcing();
                services.AddHostedViewIndexer<AccountView>();
                services.AddHostedViewIndexer<SearchAccountView>();
            })
            .UseSerilog((_, config) =>
            {
                config.WriteTo.Console()
                    .WriteTo.TestOutput(output, LogEventLevel.Information);
            })
            .UseEnvironment("test")
            .Build()
            .AddTestBucketIds();

        _streamingClient = _host.Services.GetRequiredService<StreamingClient>();
        _streamFactory = _host.Services.GetRequiredService<IStreamFactory>();
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
        foreach (var topic in _streamFactory.GetTopicResolver().GetTopics<Event>(typeof(SearchAccountView)))
            await _streamFactory.CreateAdmin().DeleteTopicAsync(topic, CancellationToken.None);
        foreach (var topic in _streamFactory.GetTopicResolver().GetTopics<Event>(typeof(User)))
            await _streamFactory.CreateAdmin().DeleteTopicAsync(topic, CancellationToken.None);
        foreach (var topic in _streamFactory.GetTopicResolver().GetTopics<Event>(typeof(Account)))
            await _streamFactory.CreateAdmin().DeleteTopicAsync(topic, CancellationToken.None);
        await _host.StopAsync();
        _host.Dispose();
    }

    [Fact]
    public async Task TestViewIndexer()
    {
        var streamId = "123";
        var publisher = _streamingClient.CreatePublisher<Event>().AddTopic<Account>().Build();

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
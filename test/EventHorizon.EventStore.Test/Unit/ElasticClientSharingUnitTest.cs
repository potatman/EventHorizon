using EventHorizon.Abstractions.Extensions;
using EventHorizon.Abstractions.Interfaces;
using EventHorizon.EventStore.ElasticSearch;
using EventHorizon.EventStore.ElasticSearch.Extensions;
using EventHorizon.EventStore.Interfaces.Factory;
using EventHorizon.EventStore.Test.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventHorizon.EventStore.Test.Unit;

[Trait("Category", "Unit")]
public class ElasticClientSharingUnitTest
{
    [Fact]
    public void FactoriesInOneProviderShareOneClient()
    {
        var provider = BuildProvider();

        var snapshot = (ElasticStoreFactory<ExampleStoreState>)provider.GetRequiredService<ISnapshotStoreFactory<ExampleStoreState>>();
        var lockStore = (ElasticStoreFactory<ExampleStoreState>)provider.GetRequiredService<ILockStoreFactory<ExampleStoreState>>();
        var otherView = (ElasticStoreFactory<OtherState>)provider.GetRequiredService<IViewStoreFactory<OtherState>>();

        Assert.Same(snapshot.Client, lockStore.Client);
        Assert.Same(snapshot.Client, otherView.Client);
    }

    [Fact]
    public void SeparateProvidersGetSeparateClients()
    {
        var first = (ElasticStoreFactory<ExampleStoreState>)BuildProvider().GetRequiredService<ISnapshotStoreFactory<ExampleStoreState>>();
        var second = (ElasticStoreFactory<ExampleStoreState>)BuildProvider().GetRequiredService<ISnapshotStoreFactory<ExampleStoreState>>();

        Assert.NotSame(first.Client, second.Client);
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventHorizon(x => x
            .AddElasticSnapshotStore(c => c.Uris = new[] { "http://localhost:9200" })
            .AddElasticViewStore(c => c.Uris = new[] { "http://localhost:9200" }));
        return services.BuildServiceProvider();
    }

    public class OtherState : IState
    {
        public string Id { get; set; }
    }
}

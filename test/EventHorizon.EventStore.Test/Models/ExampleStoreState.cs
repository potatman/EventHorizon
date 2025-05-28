using EventHorizon.Abstractions.Attributes;
using EventHorizon.Abstractions.Interfaces;
using EventHorizon.EventStore.ElasticSearch.Attributes;

namespace EventHorizon.EventStore.Test.Models;

[SnapshotStore("test_snapshot_example")]
[ElasticIndex(Refresh = "true")]
public class ExampleStoreState : IState
{
    public string Id { get; set; }
    public string Name { get; set; }
}

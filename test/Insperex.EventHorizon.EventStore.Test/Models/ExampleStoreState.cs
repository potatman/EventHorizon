using Insperex.EventHorizon.Abstractions.Attributes;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.EventStore.ElasticSearch.Attributes;

namespace Insperex.EventHorizon.EventStore.Test.Models;

[SnapshotStore("test_snapshot_example")]
[ElasticIndex(Refresh = "true")]
public class ExampleStoreState : IState
{
    public string Id { get; set; }
    public string Name { get; set; }
}

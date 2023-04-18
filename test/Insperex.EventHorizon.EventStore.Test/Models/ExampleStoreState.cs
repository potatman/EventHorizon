using Insperex.EventHorizon.Abstractions.Attributes;
using Insperex.EventHorizon.Abstractions.Interfaces;

namespace Insperex.EventHorizon.EventStore.Test.Models;

[SnapshotStore("test_snapshot_example", nameof(ExampleStoreState))]
[EventStream("test_event_example", nameof(ExampleStoreState))]
public class ExampleStoreState : IState
{
    public string Id { get; set; }
    public string Name { get; set; }
}
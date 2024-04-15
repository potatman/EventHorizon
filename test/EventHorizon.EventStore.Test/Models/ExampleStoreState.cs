using EventHorizon.Abstractions.Attributes;
using EventHorizon.Abstractions.Interfaces;
using EventHorizon.EventStore.ElasticSearch.Attributes;

namespace EventHorizon.EventStore.Test.Models;

[Store("test_snapshot_example")]
[ElasticIndex(Refresh = "true")]
public class ExampleStoreState : IState
{
    public string Id { get; set; }
    public string Name { get; set; }

    public ExampleSubclass[] SubClasses { get; set; }

}

public class ExampleSubclass
{
    public string Name1 { get; set; }
    public string Name2 { get; set; }
    public string Name3 { get; set; }
    public string Name4 { get; set; }
    public string Name5 { get; set; }
    public string Name6 { get; set; }
    public string Name7 { get; set; }
    public string Name8 { get; set; }
    public string Name9 { get; set; }
    public string Name10 { get; set; }
}

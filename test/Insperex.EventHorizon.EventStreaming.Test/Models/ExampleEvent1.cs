using Insperex.EventHorizon.Abstractions.Attributes;
using Insperex.EventHorizon.Abstractions.Interfaces;

namespace Insperex.EventHorizon.EventStreaming.Test.Models;

[EventStream("test_event_example1", nameof(ExampleEvent1))]
public class ExampleEvent1 : IEvent
{
    public string StreamId { get; set; }
    public string Name { get; set; }
}
using Insperex.EventHorizon.Abstractions.Attributes;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;

namespace Insperex.EventHorizon.EventStreaming.Test.Models;

[Stream<Event>("test_event_example1")]
public class ExampleEvent1 : IEvent
{
    public string StreamId { get; set; }
    public string Name { get; set; }
}

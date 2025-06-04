using EventHorizon.Abstractions.Attributes;
using EventHorizon.Abstractions.Interfaces;
using EventHorizon.Abstractions.Interfaces.Actions;
using EventHorizon.Abstractions.Models.TopicMessages;
using EventHorizon.EventStreaming.Pulsar.Attributes;
using ProtoBuf;

namespace EventHorizon.EventStreaming.Benchmark.Models;

[Stream("benchmark")]
[PulsarNamespace("test_benchmark", "$type")]
public class ExampleEvent : IEvent
{
    public string Property1 { get; set; }
    public string Property2 { get; set; }
    public string Property3 { get; set; }
    public string Property4 { get; set; }
    public string Property5 { get; set; }
    public string Property6 { get; set; }
    public string Property7 { get; set; }
    public string Property8 { get; set; }
    public string Property9 { get; set; }
    public string Property10 { get; set; }
}

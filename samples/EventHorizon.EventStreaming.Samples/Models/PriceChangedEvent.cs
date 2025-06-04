using EventHorizon.Abstractions.Attributes;
using EventHorizon.Abstractions.Interfaces;
using EventHorizon.Abstractions.Interfaces.Actions;
using EventHorizon.Abstractions.Models.TopicMessages;
using EventHorizon.EventStreaming.Pulsar.Attributes;

namespace EventHorizon.EventStreaming.Samples.Models;

public abstract record PriceChanged(string Id, int Price) : IEvent;


[Stream("feed1")]
[PulsarNamespace("test_pricing", "$type")]
public record Feed1PriceChanged(string Id, int Price) : PriceChanged (Id, Price);

[Stream("feed2")]
[PulsarNamespace("test_pricing", "$type")]
public record Feed2PriceChanged(string Id, int Price) : PriceChanged (Id, Price);

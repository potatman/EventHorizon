using Insperex.EventHorizon.Abstractions.Attributes;
using Insperex.EventHorizon.Abstractions.Interfaces;

namespace Insperex.EventHorizon.EventStreaming.Samples.Models;

public abstract record PriceChanged(string Id, int Price) : IEvent;


[EventStream("test_raw_feed1")]
public record Feed1PriceChanged(string Id, int Price) : PriceChanged (Id, Price);


[EventStream("test_raw_feed2")]
public record Feed2PriceChanged(string Id, int Price) : PriceChanged (Id, Price);
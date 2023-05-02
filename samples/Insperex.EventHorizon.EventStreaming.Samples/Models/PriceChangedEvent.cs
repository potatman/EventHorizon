using Insperex.EventHorizon.Abstractions.Attributes;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Interfaces.Actions;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;

namespace Insperex.EventHorizon.EventStreaming.Samples.Models;

public abstract record PriceChanged(string Id, int Price) : IEvent;


[Stream<Event>("test_pricing", "raw",  "feed1")]
public record Feed1PriceChanged(string Id, int Price) : PriceChanged (Id, Price);


[Stream<Event>("test_pricing", "raw",  "feed2")]
public record Feed2PriceChanged(string Id, int Price) : PriceChanged (Id, Price);

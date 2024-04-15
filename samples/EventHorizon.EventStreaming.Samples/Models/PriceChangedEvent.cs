using EventHorizon.Abstractions.Interfaces.Actions;

namespace EventHorizon.EventStreaming.Samples.Models;

public abstract record PriceChanged(string Id, int Price) : IEvent;

public record Feed1PriceChanged(string Id, int Price) : PriceChanged (Id, Price);

public record Feed2PriceChanged(string Id, int Price) : PriceChanged (Id, Price);

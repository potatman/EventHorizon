using System;

namespace Insperex.EventHorizon.EventStore.Interfaces;

public interface ICrudEntity
{
    public string Id { get; set; }
    public DateTime UpdatedDate { get; set; }
}
using System;

namespace EventHorizon.EventStore.Interfaces;

public interface ICrudEntity
{
    public string Id { get; set; }
    public DateTime UpdatedDate { get; set; }
    public DateTime CreatedDate { get; set; }
}

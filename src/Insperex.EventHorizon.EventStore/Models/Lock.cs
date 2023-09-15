using System;
using Insperex.EventHorizon.EventStore.Interfaces;

namespace Insperex.EventHorizon.EventStore.Models;

public class Lock : ICrudEntity
{
    public DateTime Expiration { get; set; }
    public string Id { get; set; }
    public string Owner { get; set; }
    public DateTime UpdatedDate { get; set; }
    public DateTime CreatedDate { get; set; }
}

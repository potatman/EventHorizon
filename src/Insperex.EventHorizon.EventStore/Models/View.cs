using System;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Serialization;
using Insperex.EventHorizon.Abstractions.Serialization.Compression;
using Insperex.EventHorizon.EventStore.Interfaces;

namespace Insperex.EventHorizon.EventStore.Models;

public class View<T> : IStateParent<T>
    where T : class, IState
{
    public View()
    {
    }

    public View(string id, long sequenceId, T state, DateTime createdDate, DateTime updatedDate)
    {
        // Set Data
        Id = id;
        SequenceId = sequenceId;
        Payload = state;

        // Set Dates
        CreatedDate = createdDate == default ? DateTime.UtcNow : createdDate;
        UpdatedDate = updatedDate == default ? CreatedDate : updatedDate;
    }

    public string Id { get; set; }
    public long SequenceId { get; set; }
    public T Payload { get; set; }
    public Compression? Compression { get; set; }
    public byte[] Data { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime UpdatedDate { get; set; }
}

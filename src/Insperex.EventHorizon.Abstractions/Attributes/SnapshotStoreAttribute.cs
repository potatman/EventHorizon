using System;

namespace Insperex.EventHorizon.Abstractions.Attributes;

[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class SnapshotStoreAttribute : Attribute
{
    public string BucketId { get; set; }
    public string Type { get; set; }
    public bool ValidateCommandHandlers { get; set; }
    public bool ValidateRequestHandlers { get; set; }
    public bool ValidateEventHandlers { get; set; }

    public SnapshotStoreAttribute(string bucketId, string type)
    {
        BucketId = bucketId;
        Type = type;
    }
}
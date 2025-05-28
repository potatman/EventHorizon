using System;

namespace EventHorizon.Abstractions.Attributes;

[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class SnapshotStoreAttribute : Attribute
{
    public string BucketId { get; set; }

    public SnapshotStoreAttribute(string bucketId)
    {
        BucketId = bucketId;
    }
}

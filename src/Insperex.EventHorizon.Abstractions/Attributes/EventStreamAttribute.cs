using System;

namespace Insperex.EventHorizon.Abstractions.Attributes;

[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
public sealed class EventStreamAttribute : Attribute
{
    public string BucketId { get; set; }
    public string Type { get; set; }

    public EventStreamAttribute(string bucketId)
    {
        BucketId = bucketId;
    }

    public EventStreamAttribute(string bucketId, string type)
    {
        BucketId = bucketId;
        Type = type;
    }
}
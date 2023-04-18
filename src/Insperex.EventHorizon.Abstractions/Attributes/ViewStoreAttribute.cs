using System;
using System.Collections.Generic;

namespace Insperex.EventHorizon.Abstractions.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class ViewStoreAttribute : Attribute
{
    public virtual string BucketId { get; set; }
    public string Type { get; set; }
    public bool ValidateEventHandlers { get; set; }
    public ViewStoreAttribute(string bucketId, string type)
    {
        BucketId = bucketId;
        Type = type;
    }
}
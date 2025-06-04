using System;

namespace EventHorizon.Abstractions.Attributes;

[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = true)]
public sealed class StreamPartitionKeyAttribute : Attribute
{

}

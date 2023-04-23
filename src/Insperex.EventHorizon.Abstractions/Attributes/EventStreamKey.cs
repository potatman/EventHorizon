using System;

namespace Insperex.EventHorizon.Abstractions.Attributes;

[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = true)]
public sealed class EventStreamKey : Attribute
{
    
}
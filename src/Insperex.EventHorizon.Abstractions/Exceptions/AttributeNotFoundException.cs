using System;

namespace Insperex.EventHorizon.Abstractions.Exceptions;

public class AttributeNotFoundException<T> : Exception
    where T : Attribute
{
    public AttributeNotFoundException(Type type)
        : base($"Attribute {typeof(T).Name} not found for {type.Name}")
    {
    }
}
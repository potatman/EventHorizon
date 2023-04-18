using System;

namespace Insperex.EventHorizon.Abstractions.Exceptions;

public class InterfaceNotFoundException<T> : Exception
    where T : Attribute
{
    public InterfaceNotFoundException(Type type)
        : base($"Interface I{typeof(T).Name} not found for {type.Name}")
    {
    }
}
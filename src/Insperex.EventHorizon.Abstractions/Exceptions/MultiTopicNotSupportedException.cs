using System;

namespace Insperex.EventHorizon.Abstractions.Exceptions;

public class MultiTopicNotSupportedException<T> : Exception
{
    public MultiTopicNotSupportedException()
        : base($"Multi-Topic is not supported for {typeof(T).Name}")
    {
    }
}
using System;

namespace EventHorizon.Abstractions.Exceptions;

[Serializable]
public class MultiTopicNotSupportedException<T> : Exception
{
    public MultiTopicNotSupportedException()
        : base($"Multi-Topic is not supported for {typeof(T).Name}")
    {
    }
}

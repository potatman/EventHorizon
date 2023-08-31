using System;

namespace Insperex.EventHorizon.EventStreaming.Subscriptions.Backoff;

public class ConstantBackoffStrategy: IConstantBackoffStrategy
{
    /// <summary>
    /// Amount of time until the next retry.
    /// </summary>
    public TimeSpan Delay { get; set; }
}

using System;

namespace Insperex.EventHorizon.EventStreaming.Subscriptions.Backoff;

/// <summary>
/// Backoff strategy that depends upon a single (presumably repeating) delay timespan.
/// </summary>
public interface IConstantBackoffStrategy: IBackoffStrategy
{
    /// <summary>
    /// Amount of time until the next retry.
    /// </summary>
    TimeSpan Delay { get; set; }

    TimeSpan IBackoffStrategy.NextInterval(int attempts) => Delay;
}

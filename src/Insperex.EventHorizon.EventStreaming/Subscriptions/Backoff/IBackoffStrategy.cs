using System;

namespace Insperex.EventHorizon.EventStreaming.Subscriptions.Backoff;

public interface IBackoffStrategy
{
    /// <summary>
    /// Given the previous number of attempts, determine the minimum wait time until the next attempt.
    /// </summary>
    /// <param name="attempts">Number of attempts so far.</param>
    /// <returns>TimeSpan representing the minimum time to wait until the next attempt.</returns>
    TimeSpan NextInterval(int attempts);
}

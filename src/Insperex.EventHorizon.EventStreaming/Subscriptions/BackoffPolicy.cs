using System;

namespace Insperex.EventHorizon.EventStreaming.Subscriptions;

/// <summary>
/// Configure schedule for retrying some operation.
/// </summary>
public class BackoffPolicy
{
    /// <summary>
    /// Shortest possible delay in the retry schedule.
    /// </summary>
    public TimeSpan MinDelay { get; set; }
    /// <summary>
    /// Longest possible delay in the retry schedule.
    /// </summary>
    public TimeSpan MaxDelay { get; set; }
    /// <summary>
    /// How much to multiply the previous delay to calculate the next delay
    /// (but using <see cref="MaxDelay"/> as a ceiling.)
    /// </summary>
    public int Multiplier { get; set; }
}

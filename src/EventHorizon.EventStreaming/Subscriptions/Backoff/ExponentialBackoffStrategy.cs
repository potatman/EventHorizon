using System;

namespace EventHorizon.EventStreaming.Subscriptions.Backoff;

public class ExponentialBackoffStrategy: IBackoffStrategy
{
    private readonly Random _random = new Random();

    /// <summary>
    /// Base number of milliseconds' backoff.
    /// </summary>
    public int BaseMs { get; set; }

    /// <summary>
    /// Maximum backoff interval (milliseconds).
    /// </summary>
    public int MaxMs { get; set; }

    /// <summary>
    /// Used to determine "jitter", or random adjustment to next backoff interval.
    /// </summary>
    public double JitterFactor { get; set; }

    public TimeSpan NextInterval(int attempts)
    {
        if (attempts < 0) throw new ArgumentOutOfRangeException(nameof(attempts));

        var baseBackoff = Math.Pow(2, attempts) * BaseMs;
        var jitter = (int)(baseBackoff * JitterFactor);

        var interval = baseBackoff + _random.Next(-jitter, jitter);
        interval = Math.Min(interval, MaxMs);
        interval = Math.Min(interval, int.MaxValue); // Ensure the interval is within the TimeSpan range

        return TimeSpan.FromMilliseconds(interval);
    }
}

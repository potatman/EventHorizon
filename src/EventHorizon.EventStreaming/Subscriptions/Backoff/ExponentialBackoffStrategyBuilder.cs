using System;

namespace EventHorizon.EventStreaming.Subscriptions.Backoff;

public class ExponentialBackoffStrategyBuilder
{
    private const double DefaultJitter = 0.1d;

    private double _jitter;
    private int? _startMs;
    private int? _maxMs;

    public ExponentialBackoffStrategyBuilder StartAt(TimeSpan startInterval)
    {
        _startMs = (int)Math.Min(startInterval.TotalMilliseconds, int.MaxValue);
        return this;
    }

    public ExponentialBackoffStrategyBuilder Max(TimeSpan maxInterval)
    {
        _maxMs = (int)Math.Min(maxInterval.TotalMilliseconds, int.MaxValue);
        return this;
    }

    public ExponentialBackoffStrategyBuilder Jitter(double jitter = DefaultJitter)
    {
        _jitter = Math.Abs(jitter);
        return this;
    }

    public ExponentialBackoffStrategy Build()
    {
        EnsureValid();

        return new ExponentialBackoffStrategy
        {
            BaseMs = _startMs!.Value,
            MaxMs = _maxMs!.Value,
            JitterFactor = _jitter,
        };
    }

    private void EnsureValid()
    {
        if (!_startMs.HasValue)
            throw new InvalidOperationException("Must specify starting backoff interval with .StartAt(...)");

        if (!_maxMs.HasValue)
            throw new InvalidOperationException("Must specify maximum backoff interval with .Max(...)");

        if (_maxMs.Value < _startMs.Value)
            throw new InvalidOperationException(
                "Maximum interval (.Max(...)) must not be less than start interval (.StartAt(...)).");
    }
}

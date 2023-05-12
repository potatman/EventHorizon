using System;

namespace Insperex.EventHorizon.EventStreaming.Subscriptions;

/// <summary>
/// Fluent API to build <see cref="BackoffPolicy"/>.
/// </summary>
public class BackoffPolicyBuilder
{
    public static readonly TimeSpan DefaultMinDelay = TimeSpan.FromMilliseconds(10);
    public static readonly TimeSpan DefaultMaxDelay = TimeSpan.FromMinutes(60);
    public const int DefaultMultiplier = 2;

    private TimeSpan _minDelay = DefaultMinDelay;
    private TimeSpan _maxDelay = DefaultMaxDelay;
    private int _multiplier = DefaultMultiplier;

    public BackoffPolicyBuilder MinDelay(TimeSpan minDelay)
    {
        _minDelay = minDelay;
        return this;
    }

    public BackoffPolicyBuilder MaxDelay(TimeSpan maxDelay)
    {
        _maxDelay = maxDelay;
        return this;
    }

    public BackoffPolicyBuilder Multiplier(int multiplier)
    {
        _multiplier = multiplier;
        return this;
    }

    public BackoffPolicy Build()
    {
        return new BackoffPolicy
        {
            MinDelay = _minDelay,
            MaxDelay = _maxDelay,
            Multiplier = _multiplier,
        };
    }
}

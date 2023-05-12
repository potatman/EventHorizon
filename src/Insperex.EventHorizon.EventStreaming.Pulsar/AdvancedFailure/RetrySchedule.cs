using System;
using System.Collections.Generic;
using System.Linq;
using Insperex.EventHorizon.EventStreaming.Subscriptions;

namespace Insperex.EventHorizon.EventStreaming.Pulsar.AdvancedFailure;

public class RetrySchedule
{
    private static readonly TimeSpan[] DefaultIntervals = new []{TimeSpan.FromSeconds(30)};

    private TimeSpan[] _retryIntervals;

    public RetrySchedule()
    {
        _retryIntervals = DefaultIntervals.ToArray();
    }

    public RetrySchedule(BackoffPolicy backoffPolicy)
    {
        ArgumentNullException.ThrowIfNull(backoffPolicy);

        var intervals = new List<TimeSpan>();
        intervals.Add(backoffPolicy.MinDelay);

        while (intervals[^1] < backoffPolicy.MaxDelay)
        {
            var nextInterval = intervals[^1] * backoffPolicy.Multiplier;
            intervals.Add(nextInterval > backoffPolicy.MaxDelay ? backoffPolicy.MaxDelay : nextInterval);
        }

        _retryIntervals = intervals.ToArray();
    }

    public TimeSpan NextInterval(int numPreviousRetries)
    {
        return numPreviousRetries switch
        {
            < 0 => throw new ArgumentException("Must be non-negative", nameof(numPreviousRetries)),
            var i when i < _retryIntervals.Length => _retryIntervals[i],
            _ => _retryIntervals[^1],
        };
    }
}

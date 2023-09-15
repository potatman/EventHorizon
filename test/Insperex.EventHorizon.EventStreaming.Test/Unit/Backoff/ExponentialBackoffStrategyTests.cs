using System;
using Insperex.EventHorizon.EventStreaming.Subscriptions.Backoff;
using Xunit;

namespace Insperex.EventHorizon.EventStreaming.Test.Unit.Backoff;

[Trait("Category", "Unit")]
public class ExponentialBackoffStrategyTests
{
    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 20)]
    [InlineData(2, 40)]
    [InlineData(3, 80)]
    [InlineData(4, 160)]
    [InlineData(12, 40_960)]
    [InlineData(13, 50_000)]
    public void Uses_predictable_exponential_intervals(int attempts, int expectedMs)
    {
        var strategy = new ExponentialBackoffStrategyBuilder()
            .StartAt(TimeSpan.FromMilliseconds(10))
            .Max(TimeSpan.FromSeconds(50))
            .Build();

        Assert.Equal(expectedMs, (int)strategy.NextInterval(attempts).TotalMilliseconds);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 20)]
    [InlineData(2, 40)]
    [InlineData(3, 80)]
    [InlineData(4, 160)]
    [InlineData(12, 40_960)]
    [InlineData(13, 50_000)]
    public void Uses_predictable_exponential_intervals_with_jitter(int attempts, int expectedMs)
    {
        const double jitter = 0.15d;

        var strategy = new ExponentialBackoffStrategyBuilder()
            .StartAt(TimeSpan.FromMilliseconds(10))
            .Max(TimeSpan.FromSeconds(50))
            .Jitter(jitter)
            .Build();

        var actual = strategy.NextInterval(attempts).TotalMilliseconds;

        var spread = jitter * expectedMs;
        Assert.InRange(actual, expectedMs - spread, expectedMs + spread);
    }

    [Fact]
    public void Disallows_negative_input()
    {
        var strategy = new ExponentialBackoffStrategyBuilder()
            .StartAt(TimeSpan.FromMilliseconds(10))
            .Max(TimeSpan.FromSeconds(50))
            .Build();

        Assert.Throws<ArgumentOutOfRangeException>(() => strategy.NextInterval(-2));
    }

    [Fact]
    public void Requires_base_interval()
    {
        var builder = new ExponentialBackoffStrategyBuilder();
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Requires_max_interval()
    {
        var builder = new ExponentialBackoffStrategyBuilder()
            .StartAt(TimeSpan.FromMilliseconds(10));
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Requires_max_interval_to_be_greater_than_base_interval()
    {
        var builder = new ExponentialBackoffStrategyBuilder()
            .StartAt(TimeSpan.FromMilliseconds(10))
            .Max(TimeSpan.FromMilliseconds(5));
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }
}

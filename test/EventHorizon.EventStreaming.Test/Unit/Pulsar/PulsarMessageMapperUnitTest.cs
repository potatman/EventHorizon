using System;
using EventHorizon.EventStreaming.Pulsar.Utils;
using Xunit;

namespace EventHorizon.EventStreaming.Test.Unit.Pulsar;

[Trait("Category", "Unit")]
public class PulsarMessageMapperUnitTest
{
    [Fact]
    public void PublishTimestampIsUnixEpochMilliseconds()
    {
        // Pulsar timestamp seeks expect Unix epoch milliseconds, not DateTime ticks.
        var date = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var timestamp = PulsarMessageMapper.PublishTimestampFromDate(date);

        Assert.Equal(1704067200000L, timestamp);
    }

    [Fact]
    public void PublishTimestampRoundTripsThroughPublishDate()
    {
        var date = new DateTime(2025, 6, 9, 13, 45, 30, 123, DateTimeKind.Utc);

        var roundTripped = PulsarMessageMapper.PublishDateFromTimestamp(
            PulsarMessageMapper.PublishTimestampFromDate(date));

        Assert.Equal(date, roundTripped);
    }
}

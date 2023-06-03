using System;
using Insperex.EventHorizon.Abstractions.Models;
using Pulsar.Client.Common;

namespace Insperex.EventHorizon.EventStreaming.Pulsar.Utils;

public static class PulsarMessageMapper
{
    public static TopicData MapTopicData<T>(string id, Message<T> x, string topic)
    {
        return new TopicData(
            id,
            topic ?? x.MessageId.TopicName,
            PublishDateFromTimestamp(x.PublishTime));
    }

    public static DateTime PublishDateFromTimestamp(long epochMilliseconds)
    {
        var dateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(epochMilliseconds);
        return dateTimeOffset.UtcDateTime;
    }

    public static long PublishTimestampFromDate(DateTime dateTime)
    {
        var dateTimeOffset = new DateTimeOffset(dateTime);
        return dateTimeOffset.ToUnixTimeMilliseconds();
    }
}

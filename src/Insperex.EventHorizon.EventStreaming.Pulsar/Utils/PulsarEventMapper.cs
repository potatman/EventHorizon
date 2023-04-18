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
            new DateTime(x.PublishTime));
    }
}
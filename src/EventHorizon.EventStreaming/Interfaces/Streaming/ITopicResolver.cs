using System;
using EventHorizon.Abstractions.Interfaces.Internal;

namespace EventHorizon.EventStreaming.Interfaces.Streaming;

public interface ITopicResolver
{
    string[] GetTopics<TM>(Type type, string topicName = null) where TM : ITopicMessage;
}

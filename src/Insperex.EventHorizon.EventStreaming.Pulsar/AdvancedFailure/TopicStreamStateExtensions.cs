namespace Insperex.EventHorizon.EventStreaming.Pulsar.AdvancedFailure;

public static class TopicStreamStateExtensions
{
    public static string Key(this TopicStreamState topicStream) =>
        Key(topicStream.Topic, topicStream.StreamId);

    public static string Key(this (string Topic, string StreamId) topicStream) =>
        Key(topicStream.Topic, topicStream.StreamId);

    private static string Key(string topic, string streamId) => $"{topic};{streamId}";
}

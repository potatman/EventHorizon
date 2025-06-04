using EventHorizon.Abstractions.Interfaces.Internal;
using EventHorizon.EventStreaming.Admins;
using EventHorizon.EventStreaming.Interfaces.Streaming;
using EventHorizon.EventStreaming.Publishers;
using EventHorizon.EventStreaming.Readers;
using EventHorizon.EventStreaming.Subscriptions;
using Microsoft.Extensions.Logging;

namespace EventHorizon.EventStreaming;

public class StreamingClient
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly IStreamFactory _streamFactory;

    public StreamingClient(IStreamFactory streamFactory, ILoggerFactory loggerFactory)
    {
        _streamFactory = streamFactory;
        _loggerFactory = loggerFactory;
    }

    public PublisherBuilder<T> CreatePublisher<T>() where T : class, ITopicMessage, new()
    {
        return new PublisherBuilder<T>(_streamFactory, _loggerFactory);
    }

    public ReaderBuilder<T> CreateReader<T>() where T : class, ITopicMessage, new()
    {
        return new ReaderBuilder<T>(_streamFactory, _loggerFactory);
    }

    public SubscriptionBuilder<T> CreateSubscription<T>() where T : class, ITopicMessage, new()
    {
        return new SubscriptionBuilder<T>(_streamFactory, _loggerFactory);
    }

    public Admin<T> GetAdmin<T>() where T : class, ITopicMessage, new()
    {
        return new Admin<T>(_streamFactory.CreateAdmin<T>(), _streamFactory.GetTopicResolver());
    }
}

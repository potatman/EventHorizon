using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Publishers;
using Insperex.EventHorizon.EventStreaming.Readers;
using Insperex.EventHorizon.EventStreaming.Subscriptions;
using Microsoft.Extensions.Logging;

namespace Insperex.EventHorizon.EventStreaming;

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

    public ITopicAdmin GetAdmin()
    {
        return _streamFactory.CreateAdmin();
    }
}
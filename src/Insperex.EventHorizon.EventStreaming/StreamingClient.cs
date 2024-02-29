using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.EventStreaming.Admins;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Publishers;
using Insperex.EventHorizon.EventStreaming.Readers;
using Insperex.EventHorizon.EventStreaming.Subscriptions;
using Microsoft.Extensions.Logging;

namespace Insperex.EventHorizon.EventStreaming;

public class StreamingClient<TMessage>
    where TMessage : class, ITopicMessage
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly IStreamFactory<TMessage> _streamFactory;

    public StreamingClient(IStreamFactory<TMessage> streamFactory, ILoggerFactory loggerFactory)
    {
        _streamFactory = streamFactory;
        _loggerFactory = loggerFactory;
    }

    public PublisherBuilder<TMessage> CreatePublisher()
    {
        return new PublisherBuilder<TMessage>(_streamFactory, _loggerFactory);
    }

    public ReaderBuilder<TMessage> CreateReader()
    {
        return new ReaderBuilder<TMessage>(_streamFactory);
    }

    public SubscriptionBuilder<TMessage> CreateSubscription()
    {
        return new SubscriptionBuilder<TMessage>(_streamFactory, _loggerFactory);
    }

    public Admin<TMessage> GetAdmin()
    {
        return new Admin<TMessage>(_streamFactory.CreateAdmin());
    }
}

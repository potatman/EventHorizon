using Insperex.EventHorizon.Abstractions.Formatters;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.EventStreaming.Admins;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Publishers;
using Insperex.EventHorizon.EventStreaming.Readers;
using Insperex.EventHorizon.EventStreaming.Subscriptions;
using Microsoft.Extensions.Logging;

namespace Insperex.EventHorizon.EventStreaming;

public class StreamingClient
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly Formatter _formatter;
    private readonly IStreamFactory _streamFactory;

    public StreamingClient(Formatter formatter, IStreamFactory streamFactory, ILoggerFactory loggerFactory)
    {
        _formatter = formatter;
        _streamFactory = streamFactory;
        _loggerFactory = loggerFactory;
    }

    public PublisherBuilder<TMessage> CreatePublisher<TMessage>() where TMessage : class, ITopicMessage
    {
        return new PublisherBuilder<TMessage>(_formatter, _streamFactory, _loggerFactory);
    }

    public ReaderBuilder<TMessage> CreateReader<TMessage>() where TMessage : class, ITopicMessage
    {
        return new ReaderBuilder<TMessage>(_formatter, _streamFactory);
    }

    public SubscriptionBuilder<TMessage> CreateSubscription<TMessage>() where TMessage : class, ITopicMessage
    {
        return new SubscriptionBuilder<TMessage>(_formatter, _streamFactory, _loggerFactory);
    }

    public Admin<TMessage> GetAdmin<TMessage>() where TMessage : class, ITopicMessage
    {
        return new Admin<TMessage>(_formatter, _streamFactory.CreateAdmin<TMessage>());
    }
}

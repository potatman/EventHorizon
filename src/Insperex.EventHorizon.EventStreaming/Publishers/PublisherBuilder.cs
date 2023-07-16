using System;
using System.Linq;
using System.Threading;
using Insperex.EventHorizon.Abstractions.Exceptions;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Microsoft.Extensions.Logging;

namespace Insperex.EventHorizon.EventStreaming.Publishers;

public class PublisherBuilder<T> where T : class, ITopicMessage, new()
{
    private readonly IStreamFactory _factory;
    private readonly ILoggerFactory _loggerFactory;
    private string _topic;
    private TimeSpan _sendTimeout = TimeSpan.FromMinutes(2);
    private bool _isGuaranteed;
    private int _batchSize = 100;

    public PublisherBuilder(IStreamFactory factory, ILoggerFactory loggerFactory)
    {
        _factory = factory;
        _loggerFactory = loggerFactory;
    }

    internal PublisherBuilder<T> AddTopic(string topicName = null)
    {
        if (_topic != null) throw new MultiTopicNotSupportedException<PublisherBuilder<T>>();
        _topic = topicName;
        return this;
    }

    public PublisherBuilder<T> AddStream<TS>(string topicName = null)
    {
        if (_topic != null) throw new MultiTopicNotSupportedException<PublisherBuilder<T>>();
        _topic = _factory.GetTopicResolver().GetTopics<T>(typeof(TS), topicName).FirstOrDefault();
        return this;
    }

    public PublisherBuilder<T> IsGuaranteed(bool isGuaranteed)
    {
        _isGuaranteed = isGuaranteed;
        return this;
    }

    public PublisherBuilder<T> SendTimeout(TimeSpan sendTimeout)
    {
        _sendTimeout = sendTimeout;
        return this;
    }

    public PublisherBuilder<T> BatchSize(int batchSize)
    {
        _batchSize = batchSize;
        return this;
    }

    public Publisher<T> Build()
    {
        var config = new PublisherConfig
        {
            Topic = _topic,
            IsGuaranteed = _isGuaranteed,
            SendTimeout = _sendTimeout,
            BatchSize = _batchSize
        };
        var logger = _loggerFactory.CreateLogger<Publisher<T>>();

        // Create
        return new Publisher<T>(_factory, config, logger);
    }
}

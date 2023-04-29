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

    public PublisherBuilder<T> AddTopic<TS>(string topicName = null)
    {
        if (_topic != null) throw new MultiTopicNotSupportedException<PublisherBuilder<T>>();
        _topic = _factory.GetTopicResolver().GetTopics<T>(typeof(TS), topicName).FirstOrDefault();
        return this;
    }

    public Publisher<T> Build()
    {
        var config = new PublisherConfig
        {
            Topic = _topic
        };
        var logger = _loggerFactory.CreateLogger<Publisher<T>>();

        // Ensure Topic Exists
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        _factory.CreateAdmin().RequireTopicAsync(_topic, cts.Token).ConfigureAwait(false).GetAwaiter().GetResult();

        // Create
        return new Publisher<T>(_factory, config, logger);
    }
}

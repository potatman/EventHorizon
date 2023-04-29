using System;
using System.Linq;
using System.Threading;
using Insperex.EventHorizon.Abstractions.Exceptions;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Microsoft.Extensions.Logging;

namespace Insperex.EventHorizon.EventStreaming.Readers;

public class ReaderBuilder<T> where T : class, ITopicMessage, new()
{
    private readonly IStreamFactory _factory;
    private readonly ILoggerFactory _loggerFactory;
    private DateTime? _endDateTime;
    private bool _isBeginning = true;
    private DateTime? _startDateTime;
    private string[] _keys;
    private string _topic;

    public ReaderBuilder(IStreamFactory factory, ILoggerFactory loggerFactory)
    {
        _factory = factory;
        _loggerFactory = loggerFactory;
    }

    public ReaderBuilder<T> AddTopic<TS>(string topicName = null)
    {
        if (_topic != null) throw new MultiTopicNotSupportedException<ReaderBuilder<T>>();
        _topic = _factory.GetTopicResolver().GetTopics<T>(typeof(TS), topicName).FirstOrDefault();
        return this;
    }

    public ReaderBuilder<T> Keys(params string[] keys)
    {
        _keys = keys;
        return this;
    }

    public ReaderBuilder<T> StartDateTime(DateTime? startDateTime)
    {
        _startDateTime = startDateTime;
        return this;
    }

    public ReaderBuilder<T> EndDateTime(DateTime? endDateTime)
    {
        _endDateTime = endDateTime;
        return this;
    }

    public ReaderBuilder<T> IsBeginning(bool isBeginning)
    {
        _isBeginning = isBeginning;
        return this;
    }

    public Reader<T> Build()
    {
        var config = new ReaderConfig
        {
            Topic = _topic,
            Keys = _keys,
            StartDateTime = _startDateTime,
            EndDateTime = _endDateTime,
            IsBeginning = _isBeginning
        };
        var consumer = _factory.CreateReader<T>(config);

        Console.WriteLine("ReaderBuilder - 1");
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        _factory.CreateAdmin().RequireTopicAsync(_topic, cts.Token).ConfigureAwait(false).GetAwaiter().GetResult();
        Console.WriteLine("ReaderBuilder - 2");

        return new Reader<T>(consumer);
    }
}

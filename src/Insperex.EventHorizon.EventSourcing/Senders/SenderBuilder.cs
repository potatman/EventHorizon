using System;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventStreaming;
using Microsoft.Extensions.Logging;

namespace Insperex.EventHorizon.EventSourcing.Senders;

public class SenderBuilder
{
    private readonly StreamingClient _streamingClient;
    private readonly ILoggerFactory _loggerFactory;
    private Func<AggregateStatus, string, IResponse> _getErrorResult;
    private TimeSpan _timeout;

    public SenderBuilder(StreamingClient streamingClient, ILoggerFactory loggerFactory)
    {
        _streamingClient = streamingClient;
        _loggerFactory = loggerFactory;
    }

    public SenderBuilder Timeout(TimeSpan timeout)
    {
        _timeout = timeout;
        return this;
    }

    public SenderBuilder GetErrorResult(Func<AggregateStatus, string, IResponse> getErrorResult)
    {
        _getErrorResult = getErrorResult;
        return this;
    }

    public Sender Build()
    {
        var config = new SenderConfig
        {
            Timeout = _timeout,
            GetErrorResult = _getErrorResult
        };

        var logger = _loggerFactory.CreateLogger<Sender>();
        return new Sender(_streamingClient, config, logger);
    }
}
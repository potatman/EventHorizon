using System;
using System.Net;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Interfaces.Actions;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventStreaming;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Microsoft.Extensions.Logging;

namespace Insperex.EventHorizon.EventSourcing.Senders;

public class SenderBuilder
{
    private readonly SenderSubscriptionTracker _subscriptionTracker;
    private readonly StreamingClient _streamingClient;
    private Func<HttpStatusCode, string, IResponse> _getErrorResult;
    private TimeSpan _timeout = TimeSpan.FromSeconds(120);

    public SenderBuilder(SenderSubscriptionTracker subscriptionTracker, StreamingClient streamingClient)
    {
        _subscriptionTracker = subscriptionTracker;
        _streamingClient = streamingClient;
    }

    public SenderBuilder Timeout(TimeSpan timeout)
    {
        _timeout = timeout;
        return this;
    }

    public SenderBuilder GetErrorResult(Func<HttpStatusCode, string, IResponse> getErrorResult)
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

        return new Sender(_subscriptionTracker, _streamingClient, config);
    }
}

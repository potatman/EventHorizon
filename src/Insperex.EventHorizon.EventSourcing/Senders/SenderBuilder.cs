using System;
using System.Net;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Interfaces.Actions;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventStreaming;
using Microsoft.Extensions.Logging;

namespace Insperex.EventHorizon.EventSourcing.Senders;

public class SenderBuilder<TState> where TState : IState
{
    private readonly SenderSubscriptionTracker _subscriptionTracker;
    private readonly IServiceProvider _provider;
    private readonly ILoggerFactory _loggerFactory;
    private Func<Request, HttpStatusCode, string, IResponse> _getErrorResult;
    private TimeSpan _timeout = TimeSpan.FromSeconds(120);

    public SenderBuilder(SenderSubscriptionTracker subscriptionTracker, IServiceProvider provider, ILoggerFactory loggerFactory)
    {
        _subscriptionTracker = subscriptionTracker;
        _provider = provider;
        _loggerFactory = loggerFactory;
    }

    public SenderBuilder<TState> Timeout(TimeSpan timeout)
    {
        _timeout = timeout;
        return this;
    }

    public SenderBuilder<TState> GetErrorResult(Func<Request, HttpStatusCode, string, IResponse> getErrorResult)
    {
        _getErrorResult = getErrorResult;
        return this;
    }

    public Sender<TState> Build()
    {
        var config = new SenderConfig
        {
            Timeout = _timeout,
            GetErrorResult = _getErrorResult
        };

        return new Sender<TState>(_subscriptionTracker, _provider, config, _loggerFactory.CreateLogger<Sender<TState>>());
    }
}

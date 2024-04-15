using System;
using System.Net;
using EventHorizon.Abstractions.Interfaces;
using EventHorizon.Abstractions.Interfaces.Actions;
using EventHorizon.Abstractions.Models.TopicMessages;
using EventHorizon.Abstractions.Serialization.Compression;
using Microsoft.Extensions.Logging;

namespace EventHorizon.EventSourcing.Senders;

public class SenderBuilder<TState> where TState : IState
{
    private readonly SenderSubscriptionTracker _subscriptionTracker;
    private readonly IServiceProvider _provider;
    private readonly ILoggerFactory _loggerFactory;
    private Func<Request, HttpStatusCode, string, IResponse> _getErrorResult;
    private TimeSpan _timeout = TimeSpan.FromSeconds(120);
    private Compression? _compression;

    public SenderBuilder(SenderSubscriptionTracker subscriptionTracker, IServiceProvider provider, ILoggerFactory loggerFactory)
    {
        _subscriptionTracker = subscriptionTracker;
        _provider = provider;
        _loggerFactory = loggerFactory;
    }

    public SenderBuilder<TState> Compression(Compression compression)
    {
        _compression = compression;
        return this;
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
            Compression = _compression,
            GetErrorResult = _getErrorResult
        };

        return new Sender<TState>(_subscriptionTracker, _provider, config, _loggerFactory.CreateLogger<Sender<TState>>());
    }
}

using System;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStore.Interfaces;
using Insperex.EventHorizon.EventStore.Interfaces.Stores;
using Insperex.EventHorizon.EventStreaming;
using Microsoft.Extensions.Logging;

namespace Insperex.EventHorizon.EventSourcing.Aggregates;

public class AggregateBuilder<TParent, T>
    where TParent : class, IStateParent<T>, new()
    where T : class, IState
{
    private readonly AttributeUtil _attributeUtil;
    private readonly ICrudStore<TParent> _crudStore;
    private readonly ILoggerFactory _loggerFactory;
    private readonly StreamingClient _streamingClient;
    private bool _isRebuildEnabled;
    private int _retryLimit = 5;
    private Action<Aggregate<T>[]> _beforeSave;

    public AggregateBuilder(
        AttributeUtil attributeUtil,
        ICrudStore<TParent> crudStore,
        StreamingClient streamingClient,
        ILoggerFactory loggerFactory)
    {
        _attributeUtil = attributeUtil;
        _crudStore = crudStore;
        _streamingClient = streamingClient;
        _loggerFactory = loggerFactory;
    }

    public AggregateBuilder<TParent, T> IsRebuildEnabled(bool isRebuildEnabled)
    {
        _isRebuildEnabled = isRebuildEnabled;
        return this;
    }

    public AggregateBuilder<TParent, T> RetryLimit(int retryLimit)
    {
        _retryLimit = retryLimit;
        return this;
    }

    public AggregateBuilder<TParent, T> BeforeSave(Action<Aggregate<T>[]> beforeSave)
    {
        _beforeSave = beforeSave;
        return this;
    }

    public Aggregator<TParent, T> Build()
    {
        var config = new AggregateConfig<T>
        {
            IsRebuildEnabled = _isRebuildEnabled,
            RetryLimit = _retryLimit,
            BeforeSave = _beforeSave,
        };
        var logger = _loggerFactory.CreateLogger<Aggregator<TParent, T>>();
        return new Aggregator<TParent, T>(_attributeUtil, _crudStore, _streamingClient, config, logger);
    }
}
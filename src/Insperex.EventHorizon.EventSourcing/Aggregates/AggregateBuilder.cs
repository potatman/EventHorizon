using System;
using System.Threading;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.EventSourcing.Interfaces;
using Insperex.EventHorizon.EventSourcing.Util;
using Insperex.EventHorizon.EventStore.Interfaces;
using Insperex.EventHorizon.EventStore.Interfaces.Factory;
using Insperex.EventHorizon.EventStore.Interfaces.Stores;
using Insperex.EventHorizon.EventStore.Locks;
using Insperex.EventHorizon.EventStore.Models;
using Insperex.EventHorizon.EventStreaming;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Insperex.EventHorizon.EventSourcing.Aggregates;

public class AggregateBuilder<TParent, T>
    where TParent : class, IStateParent<T>, new()
    where T : class, IState
{
    private readonly ICrudStore<TParent> _crudStore;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ValidationUtil _validationUtil;
    private readonly IServiceProvider _serviceProvider;
    private readonly StreamingClient _streamingClient;
    private bool _isValidationEnabled = true;
    private bool _isRebuildEnabled;
    private int _retryLimit = 5;
    private IAggregateMiddleware<T> _middleware;
    private readonly LockFactory<T> _lockFactory;
    private int? _batchSize;

    public AggregateBuilder(
        IServiceProvider serviceProvider,
        StreamingClient streamingClient,
        ILoggerFactory loggerFactory)
    {
        _crudStore = typeof(TParent).Name == typeof(Snapshot<>).Name?
            (ICrudStore<TParent>)serviceProvider.GetRequiredService<ISnapshotStoreFactory<T>>().GetSnapshotStore() :
            (ICrudStore<TParent>)serviceProvider.GetRequiredService<IViewStoreFactory<T>>().GetViewStore();
        _lockFactory = serviceProvider.GetRequiredService<LockFactory<T>>();
        _validationUtil = serviceProvider.GetRequiredService<ValidationUtil>();
        _serviceProvider = serviceProvider;
        _streamingClient = streamingClient;
        _loggerFactory = loggerFactory;
    }

    public AggregateBuilder<TParent, T> IsRebuildEnabled(bool isRebuildEnabled)
    {
        _isRebuildEnabled = isRebuildEnabled;
        return this;
    }

    public AggregateBuilder<TParent, T> IsValidationEnabled(bool isValidationEnabled)
    {
        _isValidationEnabled = isValidationEnabled;
        return this;
    }

    public AggregateBuilder<TParent, T> RetryLimit(int retryLimit)
    {
        _retryLimit = retryLimit;
        return this;
    }

    public AggregateBuilder<TParent, T> BatchSize(int batchSize)
    {
        _batchSize = batchSize;
        return this;
    }

    public AggregateBuilder<TParent, T> UseMiddleware<TMiddle>() where TMiddle : IAggregateMiddleware<T>
    {
        using var scope = _serviceProvider.CreateScope();
        _middleware = scope.ServiceProvider.GetRequiredService<TMiddle>();
        return this;
    }

    public Aggregator<TParent, T> Build()
    {
        var config = new AggregateConfig<T>
        {
            IsValidationEnabled = _isValidationEnabled,
            IsRebuildEnabled = _isRebuildEnabled,
            RetryLimit = _retryLimit,
            Middleware = _middleware,
            BatchSize = _batchSize
        };

        // Create Store
        var @lock = _lockFactory.CreateLock($"Migrate-{typeof(T).Name}").WaitForLockAsync().GetAwaiter().GetResult();
        _crudStore.SetupAsync(CancellationToken.None).GetAwaiter().GetResult();
        @lock.DisposeAsync().GetAwaiter().GetResult();

        // Validate Handlers if Enabled
        if(config.IsValidationEnabled)
            _validationUtil.Validate<TParent, T>();

        var logger = _loggerFactory.CreateLogger<Aggregator<TParent, T>>();
        return new Aggregator<TParent, T>(_crudStore, _streamingClient, config, logger);
    }
}

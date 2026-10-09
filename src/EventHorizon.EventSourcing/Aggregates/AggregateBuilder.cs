using System;
using EventHorizon.Abstractions.Interfaces;
using EventHorizon.EventSourcing.Interfaces;
using EventHorizon.EventSourcing.Util;
using EventHorizon.EventStore.Interfaces;
using EventHorizon.EventStore.Interfaces.Factory;
using EventHorizon.EventStore.Interfaces.Stores;
using EventHorizon.EventStore.Models;
using EventHorizon.EventStreaming;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventHorizon.EventSourcing.Aggregates;

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
    private IAggregateMiddleware<T> _middleware;
    private readonly AggregateStoreSetup<TParent, T> _storeSetup;
    private int? _batchSize;

    // Handler validation is reflection over static assembly metadata, so one success holds for the process.
    private static volatile bool s_isValidated;

    public AggregateBuilder(
        IServiceProvider serviceProvider,
        StreamingClient streamingClient,
        ILoggerFactory loggerFactory)
    {
        _crudStore = typeof(TParent).Name == typeof(Snapshot<>).Name?
            (ICrudStore<TParent>)serviceProvider.GetRequiredService<ISnapshotStoreFactory<T>>().GetSnapshotStore() :
            (ICrudStore<TParent>)serviceProvider.GetRequiredService<IViewStoreFactory<T>>().GetViewStore();
        _storeSetup = serviceProvider.GetRequiredService<AggregateStoreSetup<TParent, T>>();
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

    /// <summary>
    /// Creates an <see cref="Aggregator{TParent,T}"/>. Performs no I/O and takes no lock: the store setup
    /// (index creation / migration) runs once per process, on the aggregator's first store operation or
    /// when <see cref="Aggregator{TParent,T}.EnsureStoreSetupAsync"/> is awaited.
    /// </summary>
    public Aggregator<TParent, T> Build()
    {
        var config = new AggregateConfig<T>
        {
            IsValidationEnabled = _isValidationEnabled,
            IsRebuildEnabled = _isRebuildEnabled,
            Middleware = _middleware,
            BatchSize = _batchSize
        };

        // Validate Handlers if Enabled
        if (config.IsValidationEnabled && !s_isValidated)
        {
            _validationUtil.Validate<TParent, T>();
            s_isValidated = true;
        }

        var logger = _loggerFactory.CreateLogger<Aggregator<TParent, T>>();
        var store = new SetupOnFirstUseCrudStore<TParent, T>(_crudStore, _storeSetup);
        return new Aggregator<TParent, T>(store, _streamingClient, config, logger);
    }
}

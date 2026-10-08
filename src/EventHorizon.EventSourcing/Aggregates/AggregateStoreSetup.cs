using System;
using System.Threading;
using System.Threading.Tasks;
using EventHorizon.Abstractions.Interfaces;
using EventHorizon.EventStore.Interfaces;
using EventHorizon.EventStore.Interfaces.Stores;
using EventHorizon.EventStore.Locks;
using Microsoft.Extensions.Logging;

namespace EventHorizon.EventSourcing.Aggregates;

/// <summary>
/// Runs <see cref="ICrudStore{T}.SetupAsync"/> for a <typeparamref name="TParent"/> store at most once per process.
/// Registered as a singleton so every <see cref="Aggregator{TParent,T}"/> built for the same state type shares it.
/// The distributed <c>Migrate-{T}</c> lock still serializes setup across processes.
/// </summary>
public sealed class AggregateStoreSetup<TParent, T> : IDisposable
    where TParent : class, IStateParent<T>, new()
    where T : class, IState
{
    private readonly LockFactory<T> _lockFactory;
    private readonly ILogger<AggregateStoreSetup<TParent, T>> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile bool _isComplete;

    public AggregateStoreSetup(LockFactory<T> lockFactory, ILogger<AggregateStoreSetup<TParent, T>> logger)
    {
        _lockFactory = lockFactory;
        _logger = logger;
    }

    /// <summary>
    /// True once setup has completed successfully in this process.
    /// </summary>
    public bool IsComplete => _isComplete;

    /// <summary>
    /// Runs the store setup if it has not completed yet. Concurrent callers wait for the first one;
    /// a failed setup is not cached, so the next caller retries it.
    /// </summary>
    public async Task EnsureAsync(ICrudStore<TParent> store, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(store);
        if (_isComplete)
            return;

        await _gate.WaitAsync(ct);
        try
        {
            if (_isComplete)
                return;

            await using var @lock = await _lockFactory
                .CreateLock($"Migrate-{typeof(T).Name}", Environment.MachineName)
                .WaitForLockAsync(ct);

            _logger.LogInformation("{Store} Store - Start {TParent} {T} Migration {Host}", store.GetType().Name, typeof(TParent).Name, typeof(T).Name, Environment.MachineName);
            await store.SetupAsync(ct);
            _logger.LogInformation("{Store} Store - Finished {TParent} {T} Migration {Host}", store.GetType().Name, typeof(TParent).Name, typeof(T).Name, Environment.MachineName);

            _isComplete = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Forgets that setup ran, so the next store operation runs it again (used after the store's database is dropped).
    /// </summary>
    public void Reset()
    {
        _isComplete = false;
    }

    public void Dispose()
    {
        _gate.Dispose();
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using EventHorizon.Abstractions.Interfaces;
using EventHorizon.EventStore.Interfaces;
using EventHorizon.EventStore.Interfaces.Stores;
using EventHorizon.EventStore.Models;

namespace EventHorizon.EventSourcing.Aggregates;

/// <summary>
/// Wraps an aggregate's store so the first operation runs the once-per-process store setup,
/// which keeps <see cref="AggregateBuilder{TParent,T}.Build"/> free of I/O.
/// </summary>
internal sealed class SetupOnFirstUseCrudStore<TParent, T> : ICrudStore<TParent>
    where TParent : class, IStateParent<T>, new()
    where T : class, IState
{
    private readonly ICrudStore<TParent> _inner;
    private readonly AggregateStoreSetup<TParent, T> _setup;

    public SetupOnFirstUseCrudStore(ICrudStore<TParent> inner, AggregateStoreSetup<TParent, T> setup)
    {
        _inner = inner;
        _setup = setup;
    }

    public Task SetupAsync(CancellationToken ct) => _setup.EnsureAsync(_inner, ct);

    public async Task<TParent[]> GetAllAsync(string[] ids, CancellationToken ct)
    {
        await _setup.EnsureAsync(_inner, ct);
        return await _inner.GetAllAsync(ids, ct);
    }

    public async Task<DateTime> GetLastUpdatedDateAsync(CancellationToken ct)
    {
        await _setup.EnsureAsync(_inner, ct);
        return await _inner.GetLastUpdatedDateAsync(ct);
    }

    public async Task<DbResult> InsertAsync(TParent[] objs, CancellationToken ct)
    {
        await _setup.EnsureAsync(_inner, ct);
        return await _inner.InsertAsync(objs, ct);
    }

    public async Task<DbResult> UpsertAsync(TParent[] objs, CancellationToken ct)
    {
        await _setup.EnsureAsync(_inner, ct);
        return await _inner.UpsertAsync(objs, ct);
    }

    public async Task DeleteAsync(string[] ids, CancellationToken ct)
    {
        await _setup.EnsureAsync(_inner, ct);
        await _inner.DeleteAsync(ids, ct);
    }

    public async Task DropDatabaseAsync(CancellationToken ct)
    {
        await _inner.DropDatabaseAsync(ct);
        _setup.Reset();
    }
}

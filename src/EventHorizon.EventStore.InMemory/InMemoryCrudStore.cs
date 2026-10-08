using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventHorizon.EventStore.InMemory.Databases;
using EventHorizon.EventStore.Interfaces;
using EventHorizon.EventStore.Interfaces.Stores;
using EventHorizon.EventStore.Models;

namespace EventHorizon.EventStore.InMemory;

public class InMemoryCrudStore<T> : ICrudStore<T>
    where T : class, ICrudEntity
{
    private readonly ConcurrentDictionary<string, ICrudEntity> _table;

    public InMemoryCrudStore(CrudDatabase crudDb)
    {
        _table = crudDb.CrudEntities.GetOrAdd(typeof(T).ToString(), _ => new ConcurrentDictionary<string, ICrudEntity>());
    }

    public Task SetupAsync(CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public Task<T[]> GetAllAsync(string[] ids, CancellationToken ct)
    {
        var objs = ids
            .Select(x => _table.TryGetValue(x, out var value) ? value : null)
            .OfType<T>()
            .ToArray();

        return Task.FromResult(objs);
    }

    public Task<DateTime> GetLastUpdatedDateAsync(CancellationToken ct)
    {
        var result = _table.Values
            .Select(x => x.UpdatedDate)
            .OrderByDescending(x => x)
            .FirstOrDefault();

        return Task.FromResult(result);
    }

    public Task<DbResult> InsertAsync(T[] objs, CancellationToken ct)
    {
        // TryAdd keeps insert-if-absent atomic, which the lock store relies on.
        var passed = objs.Where(obj => _table.TryAdd(obj.Id, obj)).ToArray();
        var failed = objs.Except(passed).ToArray();

        return Task.FromResult(new DbResult
        {
            FailedIds = failed.Select(x => x.Id).ToArray(),
            PassedIds = passed.Select(x => x.Id).ToArray()
        });
    }

    public Task<DbResult> UpsertAsync(T[] objs, CancellationToken ct)
    {
        try
        {
            foreach (var obj in objs)
                _table[obj.Id] = obj;

            return Task.FromResult(new DbResult
            {
                FailedIds = Array.Empty<string>(),
                PassedIds = objs.Select(x => x.Id).ToArray()
            });
        }
        catch
        {
            return Task.FromResult(new DbResult
            {
                FailedIds = objs.Select(x => x.Id).ToArray(),
                PassedIds = Array.Empty<string>()
            });
        }
    }

    public Task DeleteAsync(string[] ids, CancellationToken ct)
    {
        foreach (var id in ids)
            _table.TryRemove(id, out _);

        return Task.CompletedTask;
    }

    public Task DropDatabaseAsync(CancellationToken ct)
    {
        _table.Clear();
        return Task.CompletedTask;
    }
}

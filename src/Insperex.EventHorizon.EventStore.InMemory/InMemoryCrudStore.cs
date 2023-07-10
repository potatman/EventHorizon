using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.EventStore.InMemory.Databases;
using Insperex.EventHorizon.EventStore.Interfaces;
using Insperex.EventHorizon.EventStore.Interfaces.Stores;
using Insperex.EventHorizon.EventStore.Models;

namespace Insperex.EventHorizon.EventStore.InMemory;

public class InMemoryCrudStore<T> : ICrudStore<T>
    where T : class, ICrudEntity
{
    private readonly Dictionary<string, ICrudEntity> _table;

    public InMemoryCrudStore(CrudDatabase crudDb)
    {
        var typeArgs = typeof(T).GetGenericArguments();
        var typeName = typeArgs.Any()?  typeArgs.First().Name : typeof(T).Name;
        if (!crudDb.CrudEntities.ContainsKey(typeName))
            crudDb.CrudEntities[typeName] = new Dictionary<string, ICrudEntity>();

        _table = crudDb.CrudEntities[typeName];
    }

    public Task SetupAsync(CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public Task<T[]> GetAllAsync(string[] ids, CancellationToken ct)
    {
        var objs = ids
            .Where(x => _table.ContainsKey(x))
            .Select(x => _table[x])
            .Cast<T>()
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
        var failed = objs.Where(obj => _table.ContainsKey(obj.Id)).ToArray();
        var passed = objs.Where(x => !failed.Contains(x)).ToArray();

        foreach (var obj in passed)
            _table[obj.Id] = obj;

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
            _table.Remove(id);

        return Task.CompletedTask;
    }

    public Task DropDatabaseAsync(CancellationToken ct)
    {
        _table.Clear();
        return Task.CompletedTask;
    }
}
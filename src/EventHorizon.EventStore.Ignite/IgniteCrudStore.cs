using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Apache.Ignite.Core.Client;
using Apache.Ignite.Core.Client.Cache;
using Apache.Ignite.Core.Transactions;
using EventHorizon.EventStore.Interfaces;
using EventHorizon.EventStore.Interfaces.Stores;
using EventHorizon.EventStore.Models;

namespace EventHorizon.EventStore.Ignite;

public class IgniteCrudStore<T> : ICrudStore<T>
    where T : ICrudEntity
{
    private readonly ICacheClient<string, T> _cache;
    private readonly IIgniteClient _client;

    public IgniteCrudStore(IIgniteClient client, string bucketId)
    {
        _client = client;
        _cache = client.GetOrCreateCache<string, T>($"{bucketId}-{typeof(T).Name}");
    }

    public Task SetupAsync(CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public async Task<T[]> GetAllAsync(string[] ids, CancellationToken ct)
    {
        var keys = ids.Select(x => x).ToArray();
        var result = await _cache.GetAllAsync(keys);
        return result.Select(x => x.Value).ToArray();
    }

    public Task<DateTime> GetLastUpdatedDateAsync(CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public async Task<DbResult> InsertAsync(T[] objs, CancellationToken ct)
    {
        var result = new DbResult();

        // Check Get First
        var ids = objs.Select(x => x.Id).ToArray();
        var entries = await _cache.GetAllAsync(ids);
        var existing = entries.Select(x => x.Key).ToArray();

        // Try Insert with transaction
        using var transaction = _client.GetTransactions().TxStart(
            TransactionConcurrency.Pessimistic,
            TransactionIsolation.ReadCommitted,
            TimeSpan.FromMilliseconds(300));

        try
        {
            var failedIds = new List<string>();
            var passedIds = new List<string>();

            foreach (var obj in objs)
            {
                var exists = await _cache.PutIfAbsentAsync(obj.Id, obj);
                if (!exists)
                    failedIds.Add(obj.Id);
                else
                    passedIds.Add(obj.Id);
            }

            result.FailedIds = failedIds.ToArray();
            result.PassedIds = passedIds.ToArray();
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            result.FailedIds = objs.Select(x => x.Id).ToArray();
        }

        return result;
    }

    public async Task<DbResult> UpsertAsync(T[] objs, CancellationToken ct)
    {
        var result = new DbResult();
        var req = objs.ToDictionary(x => x.Id);

        // Try Insert with transaction
        using var transaction = _client.GetTransactions().TxStart(
            TransactionConcurrency.Pessimistic,
            TransactionIsolation.ReadCommitted,
            TimeSpan.FromMilliseconds(300));

        try
        {
            await _cache.PutAllAsync(req);
            transaction.Commit();
            result.PassedIds = objs.Select(x => x.Id).ToArray();
        }
        catch
        {
            transaction.Rollback();
            result.FailedIds = objs.Select(x => x.Id).ToArray();
        }

        return result;
    }

    public Task DeleteAsync(string[] ids, CancellationToken ct)
    {
        var keys = ids.Select(x => x).ToArray();
        return _cache.RemoveAllAsync(keys);
    }

    public Task DropDatabaseAsync(CancellationToken ct)
    {
        _client.DestroyCache(_cache.Name);
        return Task.CompletedTask;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Apache.Ignite;
using Apache.Ignite.Table;
using EventHorizon.EventStore.Interfaces;
using EventHorizon.EventStore.Interfaces.Stores;
using EventHorizon.EventStore.Models;

namespace EventHorizon.EventStore.Ignite;

public class IgniteCrudStore<T> : ICrudStore<T>
    where T : ICrudEntity
{
    private readonly ITable _table;
    private readonly IIgniteClient _client;
    private readonly string _tableName;

    public IgniteCrudStore(IIgniteClient client, string bucketId)
    {
        _client = client;
        _tableName = $"{bucketId}_{typeof(T).Name}";
        _table = client.Tables.GetTableAsync(_tableName).Result;
    }

    public Task SetupAsync(CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public async Task<T[]> GetAllAsync(string[] ids, CancellationToken ct)
    {
        var result = new List<T>();
        var keyValueView = _table.GetRecordView<T>();

        foreach (var id in ids)
        {
            // Create a key object with the Id property
            var key = Activator.CreateInstance<T>();
            key.Id = id;

            var record = await keyValueView.GetAsync(null, key);
            if (record.HasValue)
                result.Add(record.Value);
        }

        return result.ToArray();
    }

    public Task<DateTime> GetLastUpdatedDateAsync(CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public async Task<DbResult> InsertAsync(T[] objs, CancellationToken ct)
    {
        var result = new DbResult();
        var keyValueView = _table.GetRecordView<T>();

        // Try Insert with transaction
        var transaction = await _client.Transactions.BeginAsync();

        try
        {
            var failedIds = new List<string>();
            var passedIds = new List<string>();

            foreach (var obj in objs)
            {
                var inserted = await keyValueView.InsertAsync(transaction, obj);
                if (!inserted)
                    failedIds.Add(obj.Id);
                else
                    passedIds.Add(obj.Id);
            }

            result.FailedIds = failedIds.ToArray();
            result.PassedIds = passedIds.ToArray();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            result.FailedIds = objs.Select(x => x.Id).ToArray();
        }

        return result;
    }

    public async Task<DbResult> UpsertAsync(T[] objs, CancellationToken ct)
    {
        var result = new DbResult();
        var keyValueView = _table.GetRecordView<T>();

        // Try Insert with transaction
        var transaction = await _client.Transactions.BeginAsync();

        try
        {
            foreach (var obj in objs)
            {
                await keyValueView.UpsertAsync(transaction, obj);
            }

            await transaction.CommitAsync();
            result.PassedIds = objs.Select(x => x.Id).ToArray();
        }
        catch
        {
            await transaction.RollbackAsync();
            result.FailedIds = objs.Select(x => x.Id).ToArray();
        }

        return result;
    }

    public async Task DeleteAsync(string[] ids, CancellationToken ct)
    {
        var keyValueView = _table.GetRecordView<T>();

        foreach (var id in ids)
        {
            // Create a key object with the Id property
            var key = Activator.CreateInstance<T>();
            key.Id = id;

            await keyValueView.DeleteAsync(null, key);
        }
    }

    public async Task DropDatabaseAsync(CancellationToken ct)
    {
        await _client.Sql.ExecuteAsync(null, $"DROP TABLE IF EXISTS {_tableName}");
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Serialization.Compression;
using Insperex.EventHorizon.EventStore.Models;

namespace Insperex.EventHorizon.EventStore.Interfaces.Stores;

public interface ICrudStore<T>
    where T : ICrudEntity
{
    Task MigrateAsync(CancellationToken ct);
    public Task<T[]> GetAllAsync(string[] ids, CancellationToken ct);
    public Task<DbResult> InsertAllAsync(T[] objs, CancellationToken ct);
    public Task<DbResult> UpsertAllAsync(T[] objs, CancellationToken ct);
    public Task DeleteAllAsync(string[] ids, CancellationToken ct);
    public Task DropDatabaseAsync(CancellationToken ct);
}

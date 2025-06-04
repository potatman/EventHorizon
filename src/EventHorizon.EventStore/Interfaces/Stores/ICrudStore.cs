using System;
using System.Threading;
using System.Threading.Tasks;
using EventHorizon.EventStore.Models;

namespace EventHorizon.EventStore.Interfaces.Stores;

public interface ICrudStore<T>
    where T : ICrudEntity
{
    Task SetupAsync(CancellationToken ct);
    public Task<T[]> GetAllAsync(string[] ids, CancellationToken ct);
    Task<DateTime> GetLastUpdatedDateAsync(CancellationToken ct);
    public Task<DbResult> InsertAsync(T[] objs, CancellationToken ct);
    public Task<DbResult> UpsertAsync(T[] objs, CancellationToken ct);
    public Task DeleteAsync(string[] ids, CancellationToken ct);
    public Task DropDatabaseAsync(CancellationToken ct);
}

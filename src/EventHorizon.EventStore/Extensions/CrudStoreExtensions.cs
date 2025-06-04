using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventHorizon.EventStore.Interfaces;
using EventHorizon.EventStore.Interfaces.Stores;

namespace EventHorizon.EventStore.Extensions;

public static class CrudStoreExtensions
{
    public static async Task<T> GetAsync<T>(this ICrudStore<T> crudStore, string id, CancellationToken ct)
        where T : ICrudEntity
    {
        var result = await crudStore.GetAllAsync(new[] { id }, ct);
        return result.FirstOrDefault();
    }
}

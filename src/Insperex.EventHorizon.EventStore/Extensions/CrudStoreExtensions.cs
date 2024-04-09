using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.EventStore.Interfaces;
using Insperex.EventHorizon.EventStore.Interfaces.Stores;

namespace Insperex.EventHorizon.EventStore.Extensions;

public static class CrudStoreExtensions
{
    public static async Task<T> GetOneAsync<T>(this ICrudStore<T> crudStore, string id, CancellationToken ct)
        where T : ICrudEntity
    {
        var result = await crudStore.GetAllAsync([id], ct).ConfigureAwait(false);
        return result.FirstOrDefault();
    }

    public static Task DeleteOneAsync<T>(this ICrudStore<T> crudStore, string id, CancellationToken ct)
        where T : ICrudEntity
    {
        return crudStore.DeleteAllAsync([id], ct);
    }
}

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Serialization.Compression;
using Insperex.EventHorizon.Abstractions.Serialization.Compression.Extensions;
using Insperex.EventHorizon.EventStore.Interfaces;
using Insperex.EventHorizon.EventStore.Interfaces.Stores;
using Insperex.EventHorizon.EventStore.Models;

namespace Insperex.EventHorizon.EventStore
{
    public class Store<TEntity, T>
        where TEntity : ICrudEntity
        where T : class
    {
        private readonly StoreConfig _config;
        private readonly ICrudStore<TEntity> _crudStore;

        public Store(StoreConfig config, ICrudStore<TEntity> crudStore)
        {
            _config = config;
            _crudStore = crudStore;
        }

        public Task MigrateAsync(CancellationToken ct) => _crudStore.MigrateAsync(ct);


        public async Task<TEntity> GetAsync(string id, CancellationToken ct)
        {
            var results = await GetAllAsync([id], ct);
            return results.FirstOrDefault();
        }

        public async Task<TEntity[]> GetAllAsync(string[] ids, CancellationToken ct)
        {
            var results = await _crudStore.GetAllAsync(ids, ct);

            // Decompress
            foreach (var result in results)
                if(result is ICompressible<T> compressible)
                    compressible.Decompress();

            return results.ToArray();
        }

        public Task<DbResult> InsertAllAsync(TEntity[] objs, CancellationToken ct)
        {
            // Decompress
            foreach (var obj in objs)
                if(obj is ICompressible<T> compressible)
                    compressible.Compress(_config.CompressionType);

            return _crudStore.InsertAllAsync(objs, ct);
        }

        public Task<DbResult> UpsertAllAsync(TEntity[] objs, CancellationToken ct)
        {
            // Decompress
            foreach (var obj in objs)
                if(obj is ICompressible<T> compressible)
                    compressible.Compress(_config.CompressionType);

            return _crudStore.UpsertAllAsync(objs, ct);
        }

        public Task DeleteAllAsync(string[] ids, CancellationToken ct) => _crudStore.DeleteAllAsync(ids, ct);

        public Task DropDatabaseAsync(CancellationToken ct) => _crudStore.DropDatabaseAsync(ct);
    }
}

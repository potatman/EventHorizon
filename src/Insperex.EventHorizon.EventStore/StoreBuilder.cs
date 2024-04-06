using System;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Serialization.Compression;
using Insperex.EventHorizon.EventStore.Interfaces;
using Insperex.EventHorizon.EventStore.Interfaces.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace Insperex.EventHorizon.EventStore
{
    public class StoreBuilder<TEntity, T>
        where TEntity : ICrudEntity
        where T : class, IState
    {
        private readonly ICrudStore<TEntity> _crudStore;

        public StoreBuilder(ICrudStore<TEntity> crudStore)
        {
            _crudStore = crudStore;
        }

        private Compression? _compressionType;

        public StoreBuilder<TEntity, T> AddCompression(Compression? compressionType)
        {
            _compressionType = compressionType;
            return this;
        }

        public Store<TEntity, T> Build()
        {
            var config = new StoreConfig()
            {
                CompressionType = _compressionType,
            };
            return new Store<TEntity, T>(config, _crudStore);
        }
    }
}

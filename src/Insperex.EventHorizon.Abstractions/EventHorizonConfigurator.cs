using Insperex.EventHorizon.Abstractions.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Insperex.EventHorizon.Abstractions
{
    public class EventHorizonConfigurator
    {
        internal readonly IServiceCollection Collection;
        public EventHorizonConfigurator(IServiceCollection collection)
        {
            Collection = collection;
        }

        public void AddClientResolver<T, TClient>()
            where T : class, IClientResolver<TClient>
            where TClient : class
        {
            Collection.AddSingleton<T>();
            Collection.AddSingleton(x => x.GetRequiredService<T>().GetClient());
        }
    }
}

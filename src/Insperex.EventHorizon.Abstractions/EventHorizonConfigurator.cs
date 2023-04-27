using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Insperex.EventHorizon.Abstractions
{
    public class EventHorizonConfigurator
    {
        internal readonly IServiceCollection Collection;
        internal readonly IConfiguration Config;

        public EventHorizonConfigurator(IServiceCollection collection, IConfiguration config)
        {
            Collection = collection;
            Config = config;
        }
    }
}

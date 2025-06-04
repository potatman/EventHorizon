using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventHorizon.Abstractions
{
    public class EventHorizonConfigurator
    {
        internal readonly IServiceCollection Collection;
        public EventHorizonConfigurator(IServiceCollection collection)
        {
            Collection = collection;
        }
    }
}

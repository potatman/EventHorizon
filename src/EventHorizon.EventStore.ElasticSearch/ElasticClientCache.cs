using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using EventHorizon.EventStore.ElasticSearch.Models;

namespace EventHorizon.EventStore.ElasticSearch;

/// <summary>
/// One <see cref="ElasticsearchClient"/> (and so one connection pool) per <see cref="ElasticConfig"/> instance.
/// The options system hands every <see cref="ElasticStoreFactory{T}"/> in a service provider the same config
/// instance, so all state types share a client; separate providers (or configs) get separate clients.
/// Entries are weakly keyed and disappear with their config.
/// </summary>
internal static class ElasticClientCache
{
    private static readonly ConditionalWeakTable<ElasticConfig, Lazy<ElasticsearchClient>> Clients = new();

    public static ElasticsearchClient Get(ElasticConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        // Lazy so concurrent first calls for one config never build two clients.
        return Clients.GetValue(config, c => new Lazy<ElasticsearchClient>(() => Create(c), LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    private static ElasticsearchClient Create(ElasticConfig config)
    {
        var connectionPool = new StickyNodePool(config.Uris.Select(u => new Uri(u)));
        var settings = new ElasticsearchClientSettings(connectionPool)
            .PingTimeout(TimeSpan.FromSeconds(10))
            .DeadTimeout(TimeSpan.FromSeconds(60))
            .RequestTimeout(TimeSpan.FromSeconds(60));

        if (config.UserName != null && config.Password != null)
            settings = settings.Authentication(new BasicAuthentication(config.UserName, config.Password));

        return new ElasticsearchClient(settings);
    }
}

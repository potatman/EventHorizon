using System;
using System.Linq;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.EventStore.ElasticSearch.Models;
using Microsoft.Extensions.Options;

namespace Insperex.EventHorizon.EventStore.ElasticSearch
{
    public class ElasticClientResolver : IClientResolver<ElasticsearchClient>
    {
        private readonly IOptions<ElasticConfig> _options;

        public ElasticClientResolver(IOptions<ElasticConfig> options)
        {
            _options = options;
        }

        public ElasticsearchClient GetClient()
        {
            // Client Configuration
            var connectionPool = new StickyNodePool(_options.Value.Uris.Select(u => new Uri(u)));
            var settings = new ElasticsearchClientSettings(connectionPool)
                    .PingTimeout(TimeSpan.FromSeconds(10))
                    .DeadTimeout(TimeSpan.FromSeconds(60))
                    .RequestTimeout(TimeSpan.FromSeconds(60))
                ;

            if (_options.Value.UserName != null && _options.Value.Password != null)
                settings = settings.Authentication(new BasicAuthentication(_options.Value.UserName, _options.Value.Password));

            return new ElasticsearchClient(settings);
        }
    }
}

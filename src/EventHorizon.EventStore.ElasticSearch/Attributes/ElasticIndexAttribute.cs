using System;
using Elastic.Clients.Elasticsearch;

namespace EventHorizon.EventStore.ElasticSearch.Attributes
{
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public class ElasticIndexAttribute : Attribute
    {
        public string Refresh { get; set; }
        public int RefreshIntervalMs { get; set; }
        public int Shards { get; set; }
        public int Replicas { get; set; }
        public int MaxResultWindow { get; set; }

        public static Refresh GetRefresh(string refresh) => refresh == null ? Elastic.Clients.Elasticsearch.Refresh.False : new Refresh(refresh);
    }
}

using System;
using Elastic.Clients.Elasticsearch;

namespace EventHorizon.EventStore.ElasticSearch.Attributes
{
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public class ElasticIndexAttribute : Attribute
    {
        public Refresh Refresh { get; set; } = Refresh.False;
        public int RefreshIntervalMs { get; set; }
        public int Shards { get; set; }
        public int Replicas { get; set; }
        public int MaxResultWindow { get; set; }
    }
}

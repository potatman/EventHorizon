using System;
using EventHorizon.EventStore.MongoDb.Models;
using MongoDB.Driver;

namespace EventHorizon.EventStore.MongoDb.Attributes
{
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public class MongoCollectionAttribute : Attribute
    {
        public int TimeToLiveMs { get; set; }
        public ReadPreferenceMode ReadPreferenceMode { get; set; }
        public ReadConcernLevel ReadConcernLevel { get; set; }
        public WriteConcernLevel WriteConcernLevel { get; set; }
    }
}

using System;
using MongoDB.Driver;

namespace Insperex.EventHorizon.EventStore.MongoDb.Attributes
{
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public class MongoConfigAttribute : Attribute
    {
        public int TimeToLiveMs { get; set; }
        public string TimeToLiveKey { get; set; }
        public ReadPreference ReadPreference { get; set; }
        public ReadConcern ReadConcern { get; set; }
        public WriteConcern WriteConcern { get; set; }
    }
}

using System;
using EventHorizon.EventStore.MongoDb.Models;
using MongoDB.Driver;

namespace EventHorizon.EventStore.MongoDb.Attributes
{
    /// <summary>
    /// Collection settings for the MongoDB snapshot and view stores of the state class it is placed on.
    /// Only the settings that are assigned apply; the rest keep the client's defaults.
    /// </summary>
    /// <example>
    /// <code>
    /// [MongoCollection(WriteConcernLevel = WriteConcernLevel.Majority, TimeToLiveMs = 86_400_000)]
    /// public class Session : IState { ... }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public class MongoCollectionAttribute : Attribute
    {
        private ReadPreferenceMode _readPreferenceMode;
        private ReadConcernLevel _readConcernLevel;
        private WriteConcernLevel _writeConcernLevel;

        /// <summary>
        /// When greater than zero, documents expire this many milliseconds after their CreatedDate (TTL index).
        /// </summary>
        public int TimeToLiveMs { get; set; }

        /// <summary>
        /// Avoid secondary reads for snapshot stores: aggregates load the snapshot, apply new messages and save it
        /// back, so a lagging secondary makes them overwrite newer state.
        /// </summary>
        public ReadPreferenceMode ReadPreferenceMode
        {
            get => _readPreferenceMode;
            set { _readPreferenceMode = value; HasReadPreferenceMode = true; }
        }

        public ReadConcernLevel ReadConcernLevel
        {
            get => _readConcernLevel;
            set { _readConcernLevel = value; HasReadConcernLevel = true; }
        }

        public WriteConcernLevel WriteConcernLevel
        {
            get => _writeConcernLevel;
            set { _writeConcernLevel = value; HasWriteConcernLevel = true; }
        }

        // Attribute properties cannot be nullable, so assignment is tracked to tell "not set" from the enum default.
        internal bool HasReadPreferenceMode { get; private set; }
        internal bool HasReadConcernLevel { get; private set; }
        internal bool HasWriteConcernLevel { get; private set; }
    }
}

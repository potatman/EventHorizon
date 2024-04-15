using EventHorizon.Abstractions.Serialization.Compression;

namespace EventHorizon.EventStore
{
    public class StoreConfig
    {
        public Compression? CompressionType { get; set; }
    }
}

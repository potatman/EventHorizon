using Insperex.EventHorizon.Abstractions.Serialization.Compression;

namespace Insperex.EventHorizon.EventStore
{
    public class StoreConfig
    {
        public Compression? CompressionType { get; set; }
    }
}

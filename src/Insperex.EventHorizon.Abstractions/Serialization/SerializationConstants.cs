using System.Collections.Generic;
using System.Collections.ObjectModel;
using Insperex.EventHorizon.Abstractions.Serialization.Compression;
using Insperex.EventHorizon.Abstractions.Serialization.Json;

namespace Insperex.EventHorizon.Abstractions.Serialization
{
    public static class SerializationConstants
    {
        public static readonly ISerializer Serializer = new SystemJsonSerializer();

        public static readonly ReadOnlyDictionary<Compression.Compression, ICompression> CompressorsByKey =
            new(new Dictionary<Compression.Compression, ICompression>
            {
                { Compression.Compression.Gzip, new GzipCompression() }
            });
    }
}

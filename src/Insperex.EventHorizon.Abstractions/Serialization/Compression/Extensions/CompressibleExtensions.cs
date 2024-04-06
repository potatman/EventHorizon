using System.Text;

namespace Insperex.EventHorizon.Abstractions.Serialization.Compression.Extensions
{
    public static class CompressibleExtensions
    {
        public static void Compress<T>(this ICompressible<T> compressible, Compression? compressionType) where T : class
        {
            if (compressionType == null || compressible.Compression != null)
                return;

            // Serialize
            var json = typeof(T) != typeof(string)
                ? SerializationConstants.Serializer.Serialize(compressible.Payload)
                : compressible.Payload as string;

            // Compress
            var compressor = SerializationConstants.CompressorsByKey[compressionType.Value];
            var bytes = Encoding.UTF8.GetBytes(json);

            // Set Fields
            compressible.Compression = compressionType;
            compressible.Data = compressor.Compress(bytes);
            compressible.Payload = null;
        }

        public static void Decompress<T>(this ICompressible<T> compressible) where T : class
        {
            if (compressible.Compression == null)
                return;

            // Decompress
            var compressor = SerializationConstants.CompressorsByKey[compressible.Compression.Value];
            var bytes = compressor.Decompress(compressible.Data);
            var json = Encoding.UTF8.GetString(bytes);

            // Deserialize
            compressible.Payload = typeof(T) != typeof(string)
                ? SerializationConstants.Serializer.Deserialize<T>(json)
                : json as T;

            // Set Fields
            compressible.Compression = null;
            compressible.Data = null;
        }
    }
}

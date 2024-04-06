using System.IO;
using System.IO.Compression;

namespace Insperex.EventHorizon.Abstractions.Serialization.Compression
{
    public class GzipCompression : ICompression
    {
        public byte[] Compress(byte[] bytes)
        {
            using var memoryStream = new MemoryStream();
            using (var gzipStream = new GZipStream(memoryStream, CompressionLevel.Optimal))
                gzipStream.Write(bytes, 0, bytes.Length);
            return memoryStream.ToArray();
        }

        public byte[] Decompress(byte[] bytes)
        {
            using var memoryStream = new MemoryStream(bytes);
            using var outputStream = new MemoryStream();
            using (var decompressStream = new GZipStream(memoryStream, CompressionMode.Decompress))
                decompressStream.CopyTo(outputStream);
            return outputStream.ToArray();
        }
    }
}

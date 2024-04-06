namespace Insperex.EventHorizon.Abstractions.Serialization.Compression
{
    public interface ICompression
    {
        public byte[] Compress(byte[] bytes);
        public byte[] Decompress(byte[] bytes);
    }
}

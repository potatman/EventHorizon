namespace Insperex.EventHorizon.Abstractions.Serialization.Compression
{
    public interface ICompressible<T> where T : class
    {
        public T Payload { get; set; }
        public Compression? Compression { get; set; }
        public byte[] Data { get; set; }
    }
}

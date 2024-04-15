using EventHorizon.Abstractions.Serialization.Compression;

namespace EventHorizon.Abstractions.Interfaces.Internal;

public interface ITopicMessage : ICompressible<string>
{
    public string StreamId { get; set; }
    public string Type { get; set; }
}

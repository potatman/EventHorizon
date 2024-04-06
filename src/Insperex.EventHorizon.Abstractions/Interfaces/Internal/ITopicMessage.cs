using Insperex.EventHorizon.Abstractions.Serialization.Compression;

namespace Insperex.EventHorizon.Abstractions.Interfaces.Internal;

public interface ITopicMessage : ICompressible<string>
{
    public string StreamId { get; set; }
    public string Type { get; set; }
}

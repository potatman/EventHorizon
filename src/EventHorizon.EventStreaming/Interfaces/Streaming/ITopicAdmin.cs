using System.Threading;
using System.Threading.Tasks;
using EventHorizon.Abstractions.Interfaces.Internal;

namespace EventHorizon.EventStreaming.Interfaces.Streaming;

public interface ITopicAdmin<T>
    where T : ITopicMessage
{
    Task RequireTopicAsync(string str, CancellationToken ct);
    Task DeleteTopicAsync(string str, CancellationToken ct);
}

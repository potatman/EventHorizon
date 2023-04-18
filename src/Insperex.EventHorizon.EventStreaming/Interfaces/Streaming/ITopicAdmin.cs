using System.Threading;
using System.Threading.Tasks;

namespace Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;

public interface ITopicAdmin
{
    Task RequireTopicAsync(string str, CancellationToken ct);
    Task DeleteTopicAsync(string str, CancellationToken ct);
}
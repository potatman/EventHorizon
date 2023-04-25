using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;

namespace Insperex.EventHorizon.Tool.LegacyMigration
{
    public interface IDataSource
    {
        Task<bool> AnyAsync(CancellationToken ct);
        IAsyncEnumerable<Event[]> GetAsyncEnumerator(CancellationToken ct = default);
        Task SaveState(CancellationToken ct);
        Task DeleteState(CancellationToken ct);
    }
}

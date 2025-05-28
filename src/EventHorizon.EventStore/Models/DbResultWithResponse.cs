
namespace EventHorizon.EventStore.Models
{
    public class DbResultWithResponse<T> : DbResult
    {
        public T[] Response { get; set; }
    }
}

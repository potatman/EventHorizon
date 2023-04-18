namespace Insperex.EventHorizon.EventStore.Models;

public class DbResult
{
    public string[] PassedIds { get; set; }
    public string[] FailedIds { get; set; }
}
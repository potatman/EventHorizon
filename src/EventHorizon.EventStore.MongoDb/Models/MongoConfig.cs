namespace EventHorizon.EventStore.MongoDb.Models;

public class MongoConfig
{
    public string ConnectionString { get; set; }

    /// <summary>
    /// When true (the default), snapshot, view and lock documents load even if they contain elements the current
    /// classes no longer declare, e.g. after a state property is removed or renamed. When false, the driver's
    /// default applies and such documents throw a <see cref="System.FormatException"/> on read.
    /// </summary>
    public bool IgnoreExtraElements { get; set; } = true;
}

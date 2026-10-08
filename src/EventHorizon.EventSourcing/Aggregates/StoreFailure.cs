namespace EventHorizon.EventSourcing.Aggregates;

/// <summary>
/// Why an aggregate failed outside its own handlers.
/// </summary>
internal enum StoreFailure
{
    None,

    /// <summary>
    /// The store rejected this aggregate's document while accepting others (e.g. a mapping error or a
    /// per-document rejection). May be permanent, so retries are bounded.
    /// </summary>
    Document,

    /// <summary>
    /// The store call or a middleware hook threw for the whole batch (e.g. the store is unreachable).
    /// </summary>
    Store
}

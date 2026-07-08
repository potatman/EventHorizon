namespace EventHorizon.EventStore.ElasticSearch.Attributes;

public enum MappingBehavior
{
    /// <summary>Static mapping when the state declares any StoreField intent, dynamic otherwise.</summary>
    Auto = 0,

    /// <summary>Legacy behavior: ElasticSearch infers every field mapping dynamically.</summary>
    Dynamic,

    /// <summary>Always generate a static mapping from the state's CLR shape and intents.</summary>
    Static
}

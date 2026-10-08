namespace EventHorizon.EventStore.ElasticSearch.Attributes;

public enum MappingBehavior
{
    /// <summary>
    /// Statically map only fields annotated with StoreField (and the objects containing them);
    /// every other field keeps ElasticSearch dynamic mapping. Without annotations the index is
    /// fully dynamic.
    /// </summary>
    Auto = 0,

    /// <summary>ElasticSearch infers every field mapping dynamically; StoreField intents are ignored.</summary>
    Dynamic,

    /// <summary>
    /// Generate a static mapping for every statically knowable field from the state's CLR shape,
    /// using conventions for unannotated fields (e.g. strings become keyword) and intents where declared.
    /// </summary>
    Static
}

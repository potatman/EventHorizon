namespace EventHorizon.Abstractions.Attributes;

/// <summary>
/// Implementation-agnostic declaration of how a state field is queried. Each event store
/// translates the intent into its own native mapping or indexing (e.g. ElasticSearch field
/// types, MongoDB secondary indexes). Stores that have no equivalent simply ignore it.
/// </summary>
public enum FieldIntent
{
    /// <summary>Store decides via its own conventions based on the CLR type.</summary>
    Default = 0,

    /// <summary>Matched by exact value (ids, codes, enums). Elastic: keyword. Mongo: secondary index.</summary>
    ExactMatch,

    /// <summary>Searched as analyzed text (names, descriptions). Elastic: text. Mongo: text index.</summary>
    FullText,

    /// <summary>Sorted or range-queried. Elastic: keyword/native type. Mongo: secondary index.</summary>
    Sortable,

    /// <summary>Persisted and returned, but never queried. Elastic: not indexed. Mongo: no index.</summary>
    NotQueried
}

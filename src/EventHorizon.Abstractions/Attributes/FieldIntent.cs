using System;

namespace EventHorizon.Abstractions.Attributes;

/// <summary>
/// Implementation-agnostic declaration of how a state field is queried. Each event store
/// translates the intent into its own native mapping or indexing (e.g. ElasticSearch field
/// types, MongoDB secondary indexes). Stores that have no equivalent simply ignore it.
/// </summary>
/// <remarks>
/// Intents combine, e.g. <c>FieldIntent.FullText | FieldIntent.ExactMatch</c> for a field that is
/// searched as text and also filtered, sorted or aggregated by exact value.
/// <see cref="NotQueried"/> cannot be combined with any other intent.
/// </remarks>
[Flags]
public enum FieldIntent
{
    /// <summary>Store decides via its own conventions based on the CLR type.</summary>
    Default = 0,

    /// <summary>Matched by exact value (ids, codes, enums). Elastic: keyword. Mongo: secondary index.</summary>
    ExactMatch = 1,

    /// <summary>Searched as analyzed text (names, descriptions). Elastic: text. Mongo: text index.</summary>
    FullText = 2,

    /// <summary>Sorted or range-queried. Elastic: keyword/native type. Mongo: secondary index.</summary>
    Sortable = 4,

    /// <summary>
    /// Persisted and returned, but never queried. Elastic: not indexed. Mongo: no index.
    /// Cannot be combined with other intents.
    /// </summary>
    NotQueried = 8
}

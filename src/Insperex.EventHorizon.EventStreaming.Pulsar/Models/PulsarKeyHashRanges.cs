using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avro.File;
using Range = Pulsar.Client.Api.Range;

namespace Insperex.EventHorizon.EventStreaming.Pulsar.Models
{
    /// <summary>
    /// Models the set of hash ranges allocated to a consumer of a key_shared
    /// subscription.
    /// </summary>
    public sealed class PulsarKeyHashRanges: IEquatable<PulsarKeyHashRanges>
    {
        public (int, int)[] Ranges { get; init; }

        public bool Equals(PulsarKeyHashRanges other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;

            var otherRanges = other.Ranges;
            if (Ranges == null && otherRanges == null) return true;
            if (Ranges == null || otherRanges == null) return false;
            if (Ranges.Length != otherRanges.Length) return false;

            return !Ranges.Where((t, i) => t != otherRanges[i]).Any();
        }

        public Range[] ToRangeArray()
        {
            return this.Ranges
                .Select(r => new Range(r.Item1, r.Item2))
                .ToArray();
        }

        /// <summary>
        /// Create an instance of <see cref="PulsarKeyHashRanges"/> from the JSON output
        /// of the stats query in the Pulsar Admin API./>
        /// </summary>
        /// <param name="keyHashRanges">
        /// JSON elements - each being string containing serialized JSON array of two numbers.
        /// (e.g. "[445382,445383]"
        /// </param>
        /// <returns>New instance of <see cref="PulsarKeyHashRanges"/>.</returns>
        public static PulsarKeyHashRanges Create(JsonElement[] keyHashRanges)
        {
            ArgumentNullException.ThrowIfNull(keyHashRanges);

            var ranges = keyHashRanges
                .Select(r => JsonValue.Parse(r.GetString()).AsArray().ToArray())
                .Select(r => (r[0].GetValue<int>(), r[1].GetValue<int>()))
                .ToArray();

            return new PulsarKeyHashRanges {Ranges = ranges};
        }
    }
}

using System;
using System.Linq;
using Pulsar.Client.Common;
using Range = Pulsar.Client.Api.Range;

namespace EventHorizon.EventStreaming.Pulsar.Models
{
    /// <summary>
    /// Models the set of hash ranges allocated to a consumer of a key_shared
    /// subscription.
    /// </summary>
    public sealed class PulsarKeyHashRanges: IEquatable<PulsarKeyHashRanges>
    {
        private readonly (int, int)[] _sortedRanges;

        public (int, int)[] Ranges
        {
            get => _sortedRanges;
            init
            {
                _sortedRanges = value
                    .OrderBy(v => v.Item1)
                    .ToArray();
            }
        }

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

        /// <summary>
        /// Return true if the given key is part of the hash ranges.
        /// </summary>
        public bool IsMatch(string key)
        {
            if (Ranges == null || Ranges.Length == 0) return true;
            var hash = MurmurHash3.Hash(key) % 65536;
            return IsMatch(hash);
        }

        public Range[] ToRangeArray()
        {
            return this.Ranges
                .Select(r => new Range(r.Item1, r.Item2))
                .ToArray();
        }

        private bool IsMatch(int hash)
        {
            // Binary search to find range where hash would fit.

            var left = 0;
            var right = _sortedRanges.Length - 1;

            while (left <= right)
            {
                var midPoint = left + ((right - left) / 2);
                var rangeToCheck = _sortedRanges[midPoint];

                if (hash >= rangeToCheck.Item1 && hash <= rangeToCheck.Item2)
                {
                    // Match!
                    return true;
                }
                else if (hash < rangeToCheck.Item1)
                {
                    // Focus search to the left.
                    right = midPoint - 1;
                }
                else
                {
                    // Focus search to the right.
                    left = midPoint + 1;
                }
            }

            return false;
        }
    }
}

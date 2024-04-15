using System;
using System.Collections.Generic;

namespace EventHorizon.Abstractions.Extensions
{
    public static class DictionaryExtensions
    {
        public static void AddRange<T>(this ICollection<T> target, IEnumerable<T> source)
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(source);
            foreach (var element in source)
                if(!target.Contains(element))
                    target.Add(element);
        }
    }
}

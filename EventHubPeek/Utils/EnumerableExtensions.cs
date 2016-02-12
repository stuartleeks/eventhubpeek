using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EventHubPeek.Utils
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<T> Prepend<T>(this IEnumerable<T> source, T itemToPrepend)
        {
            yield return itemToPrepend;
            foreach (var item in source)
            {
                yield return item;
            }
        }

    }
}
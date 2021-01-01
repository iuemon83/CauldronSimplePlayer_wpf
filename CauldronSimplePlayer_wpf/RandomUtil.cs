using System;
using System.Collections.Generic;
using System.Linq;

namespace CauldronSimplePlayer_wpf
{
    class RandomUtil
    {
        public static readonly Random Random = new Random();

        public static T RandomPick<T>(IReadOnlyList<T> source) => source.Any() ? source[Random.Next(source.Count)] : default;

    }
}

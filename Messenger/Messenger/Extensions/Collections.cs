using System;
using System.Collections.Generic;
using System.Linq;

namespace Messenger.Extensions
{
    internal static class Collections
    {
        public static IEnumerable<T> _Distinct<T>(this IEnumerable<T> source, Func<T, T, bool> equals)
        {
            var lst = new List<T>();
            foreach (var val in source)
            {
                if (lst.FirstOrDefault(tmp => equals.Invoke(val, tmp)) != null)
                    continue;
                lst.Add(val);
                yield return val;
            }
            lst.Clear();
        }

        public static IList<T> _Remove<T>(this IList<T> lst, Func<T, bool> fun)
        {
            var idx = 0;
            var res = new List<T>();
            while (idx < lst.Count)
            {
                var val = lst[idx];
                var con = fun.Invoke(val);
                if (con == true)
                {
                    res.Add(val);
                    lst.RemoveAt(idx);
                }
                else idx++;
            }
            return res;
        }
    }
}

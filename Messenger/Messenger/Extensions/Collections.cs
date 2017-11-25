using System;
using System.Collections.Generic;
using System.Linq;

namespace Messenger.Extensions
{
    internal static class Collections
    {
        /// <summary>
        /// 返回集合中所有唯一的项目
        /// </summary>
        public static List<T> DistinctEx<T>(this IEnumerable<T> source, Func<T, T, bool> equals)
        {
            var lst = new List<T>();
            foreach (var val in source)
            {
                if (lst.FirstOrDefault(tmp => equals.Invoke(val, tmp)) != null)
                    continue;
                lst.Add(val);
            }
            return lst;
        }

        /// <summary>
        /// 移除源列表中所有符合条件的项目, 返回被移除的项目
        /// </summary>
        public static List<T> RemoveEx<T>(this IList<T> lst, Func<T, bool> fun)
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

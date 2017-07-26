using System;
using System.Collections.Generic;

namespace Messenger.Foundation.Extensions
{
    public static partial class Extension
    {
        /// <summary>
        /// 解构键值对
        /// </summary>
        public static void Deconstruct<TK, TV>(this KeyValuePair<TK, TV> pair, out TK key, out TV val)
        {
            key = pair.Key;
            val = pair.Value;
        }

        /// <summary>
        /// 判断项目是否在集合中
        /// </summary>
        public static bool Contains<T>(this IEnumerable<T> source, Func<T, bool> fun)
        {
            foreach (var val in source)
                if (fun.Invoke(val))
                    return true;
            return false;
        }

        /// <summary>
        /// 查找集合并输出第一个匹配项
        /// </summary>
        public static bool TryFirst<T>(this IEnumerable<T> source, Func<T, bool> fun, out T target)
        {
            foreach (var val in source)
            {
                if (fun.Invoke(val) == false)
                    continue;
                target = val;
                return true;
            }
            target = default(T);
            return false;
        }

        /// <summary>
        /// 合并集合中条件相同的项
        /// </summary>
        public static IEnumerable<T> Distinct<T>(this IEnumerable<T> source, Func<T, T, bool> equals)
        {
            var lst = new List<T>();
            foreach (var val in source)
            {
                if (Contains(lst, tmp => equals.Invoke(val, tmp)))
                    continue;
                lst.Add(val);
                yield return val;
            }
            lst.Clear();
            yield break;
        }

        /// <summary>
        /// 移除所有条件为真的项目
        /// </summary>
        public static IList<T> Remove<T>(this IList<T> lst, Func<T, bool> fun)
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

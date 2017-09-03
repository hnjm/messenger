using System;
using System.Collections.Generic;
using System.Net;

namespace Messenger.Extensions
{
    internal static class Converts
    {
        internal static readonly IReadOnlyList<string> _units = new string[] { string.Empty, "K", "M", "G", "T", "P", "E" };

        internal static bool _GetHost(string str, out string host, out int port)
        {
            if (string.IsNullOrWhiteSpace(str))
                goto fail;
            var idx = str.LastIndexOf(':');
            if (idx < 0)
                goto fail;
            host = str.Substring(0, idx);
            if (string.IsNullOrWhiteSpace(host))
                goto fail;
            if (int.TryParse(str.Substring(idx + 1), out port) == false)
                goto fail;
            return true;

            fail:
            host = null;
            port = 0;
            return false;
        }

        /// <summary>
        /// 数据大小换算 (保留 2 位小数)
        /// </summary>
        internal static string _GetLength(long length)
        {
            if (_GetLength(length, out var len, out var pos))
                return $"{len:0.00} {pos}B";
            else return string.Empty;
        }

        /// <summary>
        /// 数据大小换算 以 1024 为单位切分大小
        /// </summary>
        /// <param name="length">数据大小</param>
        /// <param name="len">长度</param>
        /// <param name="pos">单位</param>
        /// <returns></returns>
        internal static bool _GetLength(long length, out double len, out string pos)
        {
            len = 0;
            pos = string.Empty;
            if (length < 0)
                return false;

            var tmp = length;
            var i = 0;
            while (i < _units.Count - 1)
            {
                if (tmp < (1 << 10))
                    break;
                tmp >>= 10;
                i++;
            }
            len = length / Math.Pow(1024, i);
            pos = _units[i];
            return true;
        }

        /// <summary>
        /// 尝试将一个字符串转换成 <see cref="IPEndPoint"/>
        /// </summary>
        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="FormatException"/>
        /// <exception cref="OverflowException"/>
        internal static IPEndPoint _ToEndPoint(this string str)
        {
            if (str == null)
                throw new ArgumentNullException();
            var idx = str.LastIndexOf(':');
            var add = str.Substring(0, idx);
            var pot = str.Substring(idx + 1);
            return new IPEndPoint(IPAddress.Parse(add.Trim()), int.Parse(pot.Trim()));
        }
    }
}

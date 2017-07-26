using System;
using System.Net;

namespace Messenger.Extensions
{
    internal static class Converts
    {
        public static bool GetHost(string str, out string host, out int port)
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
        public static string GetLength(long length)
        {
            if (GetLength(length, out var len, out var pos))
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
        public static bool GetLength(long length, out double len, out string pos)
        {
            len = 0;
            pos = string.Empty;
            if (length < 0)
                return false;

            string[] format = { string.Empty, "K", "M", "G", "T", "P", "E" };
            var tmp = length;
            var i = 0;
            while (i < format.Length - 1)
            {
                if (tmp < (1 << 10))
                    break;
                tmp >>= 10;
                i++;
            }
            len = length / Math.Pow(1024, i);
            pos = format[i];
            return true;
        }

        /// <summary>
        /// 尝试将一个字符串转换成 <see cref="IPEndPoint"/>
        /// </summary>
        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="FormatException"/>
        /// <exception cref="OverflowException"/>
        public static IPEndPoint ToEndPoint(this string str)
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

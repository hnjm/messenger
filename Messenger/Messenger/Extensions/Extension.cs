using Mikodev.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Messenger.Extensions
{
    internal static class Extension
    {
        internal static readonly IReadOnlyList<string> _units = new[] { string.Empty, "K", "M", "G", "T", "P", "E" };

        /// <summary>
        /// 分离主机字符串 (如 "some-host:7500" 分离成 "some-host" 和 7500)
        /// </summary>
        internal static bool ToHostEx(string str, out string host, out int port)
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
        internal static string ToUnitEx(long length)
        {
            if (ToUnitEx(length, out var len, out var pos))
                return $"{len:0.00} {pos}B";
            else return string.Empty;
        }

        /// <summary>
        /// 数据大小换算 以 1024 为单位切分大小
        /// </summary>
        /// <param name="length">数据大小</param>
        /// <param name="len">长度</param>
        /// <param name="pos">单位</param>
        internal static bool ToUnitEx(long length, out double len, out string pos)
        {
            if (length < 0)
                goto fail;
            var tmp = length;
            var idx = 0;
            while (idx < _units.Count - 1)
            {
                if (tmp < 1024)
                    break;
                tmp >>= 10;
                idx++;
            }
            len = length / Math.Pow(1024, idx);
            pos = _units[idx];
            return true;

            fail:
            len = 0;
            pos = string.Empty;
            return false;
        }

        /// <summary>
        /// 尝试将一个字符串转换成 <see cref="IPEndPoint"/>
        /// </summary>
        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="FormatException"/>
        /// <exception cref="OverflowException"/>
        internal static IPEndPoint ToEndPointEx(this string str)
        {
            if (str == null)
                throw new ArgumentNullException();
            var idx = str.LastIndexOf(':');
            var add = str.Substring(0, idx);
            var pot = str.Substring(idx + 1);
            return new IPEndPoint(IPAddress.Parse(add.Trim()), int.Parse(pot.Trim()));
        }

        /// <summary>
        /// 查找具有指定属性的方法
        /// </summary>
        /// <typeparam name="T">返回类型</typeparam>
        /// <param name="assembly">程序集</param>
        /// <param name="attribute">属性类型</param>
        /// <param name="basic">目标类型基类 (可为 null)</param>
        /// <param name="func">输出对象生成函数</param>
        internal static IEnumerable<T> FindAttribute<T>(Assembly assembly, Type attribute, Type basic, Func<Attribute, MethodInfo, Type, T> func) =>
            (basic == null
                ? assembly.GetTypes()
                : assembly.GetTypes().Where(t => t.IsSubclassOf(basic))
            ).Select(t => t.GetMethods()
                .Where(f => f.GetCustomAttributes(attribute).Any())
                .Select(f => func.Invoke(f.GetCustomAttributes(attribute).First(), f, t))
            ).SelectMany(i => i);

        /// <summary>
        /// 接收文件到指定路径 (若文件已存在则抛出异常)
        /// </summary>
        /// <param name="socket">待读取套接字</param>
        /// <param name="path">目标文件路径</param>
        /// <param name="length">目标文件长度</param>
        /// <param name="slice">每当数据写入时, 通知本次写入的数据长度</param>
        /// <param name="token">取消标志</param>
        internal static async Task ReceiveFileEx(this Socket socket, string path, long length, Action<long> slice, CancellationToken token)
        {
            if (length < 0)
                throw new ArgumentException("Receive file error!");
            var fst = new FileStream(path, FileMode.CreateNew, FileAccess.Write);

            try
            {
                while (length > 0)
                {
                    var sub = (int)Math.Min(length, Links.BufferLength);
                    var buf = await socket.ReceiveAsyncEx(sub);
                    await fst.WriteAsync(buf, 0, buf.Length, token);
                    length -= sub;
                    slice.Invoke(sub);
                }
                await fst.FlushAsync(token);
                fst.Dispose();
            }
            catch (Exception)
            {
                fst.Dispose();
                File.Delete(path);
                throw;
            }
        }

        /// <summary>
        /// 发送指定路径的文件 (若文件长度不匹配则抛出异常)
        /// </summary>
        /// <param name="socket">待写入套接字</param>
        /// <param name="path">源文件路径</param>
        /// <param name="length">源文件长度</param>
        /// <param name="slice">每当数据发出时, 通知本次发出的数据长度</param>
        /// <param name="token">取消标志</param>
        internal static async Task SendFileEx(this Socket socket, string path, long length, Action<long> slice, CancellationToken token)
        {
            var fst = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            try
            {
                if (fst.Length != length)
                    throw new ArgumentException("File length not match!");
                var buf = new byte[Links.BufferLength];
                while (length > 0)
                {
                    var min = (int)Math.Min(length, Links.BufferLength);
                    var sub = await fst.ReadAsync(buf, 0, min, token);
                    await socket.SendAsyncEx(buf, 0, sub);
                    length -= sub;
                    slice.Invoke(sub);
                }
            }
            finally
            {
                fst.Dispose();
            }
        }
    }
}

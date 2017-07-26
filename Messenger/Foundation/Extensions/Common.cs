using System;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;

namespace Messenger.Foundation.Extensions
{
    /// <summary>
    /// 静态扩展类
    /// </summary>
    public static partial class Extension
    {
        /// <summary>
        /// 将结构体转换成字节数组
        /// </summary>
        /// <param name="str">源对象</param>
        public static byte[] ToBytes<T>(this T str) where T : struct
        {
            var len = Marshal.SizeOf(str.GetType());
            var buf = new byte[len];
            var ptr = IntPtr.Zero;
            try
            {
                ptr = Marshal.AllocHGlobal(len);
                Marshal.StructureToPtr(str, ptr, true);
                Marshal.Copy(ptr, buf, 0, len);
                return buf;
            }
            finally
            {
                if (ptr != IntPtr.Zero)
                    Marshal.FreeHGlobal(ptr);
            }
        }

        /// <summary>
        /// 从字节流中解析出指定类型的结构体
        /// </summary>
        /// <typeparam name="T">目标类型</typeparam>
        /// <param name="buffer">源字节流</param>
        /// <param name="offset">源字节流起始索引</param>
        public static T ToStruct<T>(this byte[] buffer, int offset = 0) where T : struct
        {
            var len = Marshal.SizeOf(typeof(T));
            var ptr = IntPtr.Zero;
            try
            {
                ptr = Marshal.AllocHGlobal(len);
                Marshal.Copy(buffer, offset, ptr, len);
                return Marshal.PtrToStructure<T>(ptr);
            }
            finally
            {
                if (ptr != IntPtr.Zero)
                    Marshal.FreeHGlobal(ptr);
            }
        }

        /// <summary>
        /// 在后台线程上执行操作 并在超时或内部错误时抛出异常 (请手动终止线程)
        /// </summary>
        /// <param name="action">待执行操作</param>
        /// <param name="timeout">超时时长 (毫秒)</param>
        /// <exception cref="ApplicationException"></exception>
        /// <exception cref="TimeoutException"></exception>
        public static void TimeoutInvoke(this Action action, int timeout)
        {
            var exc = default(Exception);
            var thd = new Thread(() =>
            {
                try
                {
                    action.Invoke();
                }
                catch (Exception e)
                {
                    exc = e;
                }
            });
            thd.Start();
            if (thd.Join(timeout) == false)
                throw new TimeoutException("委托执行超时.");
            else if (exc != null)
                throw new ApplicationException($"委托内部异常, 请检查 {nameof(Exception.InnerException)} 属性.", exc);
            else return;
        }

        /// <summary>
        /// Run an delegate with try-finally, run next delegate if fail
        /// </summary>
        public static void Invoke(Action act, Action fail)
        {
            var tag = false;
            try
            {
                act.Invoke();
                tag = true;
            }
            finally
            {
                if (tag == false)
                    fail.Invoke();
            }
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

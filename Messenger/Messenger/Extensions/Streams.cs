using Mikodev.Network;
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Messenger.Extensions
{
    internal static class Streams
    {
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
            var idx = 0L;
            var fst = new FileStream(path, FileMode.CreateNew, FileAccess.Write);

            try
            {
                while (idx < length)
                {
                    var sub = (int)Math.Min(length - idx, Links.BufferLength);
                    var buf = await socket.ReceiveAsyncEx(sub);
                    await fst.WriteAsync(buf, 0, buf.Length, token);
                    idx += sub;
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
            var idx = 0L;
            var fst = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            try
            {
                if (fst.Length != length)
                    throw new ArgumentException("File length not match!");
                var buf = new byte[Links.BufferLength];
                while (idx < length)
                {
                    var len = (int)Math.Min(length - idx, Links.BufferLength);
                    var sub = await fst.ReadAsync(buf, 0, len, token);
                    await socket.SendAsyncEx(buf, 0, sub);
                    idx += sub;
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

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
        /// <param name="slice">每当写入文件时, 反馈本次写入的数据长度</param>
        /// <param name="token">取消标志</param>
        internal static async Task _ReceiveFile(this Socket socket, string path, long length, Action<long> slice, CancellationToken token)
        {
            if (string.IsNullOrEmpty(path) || length < 0)
                throw new ArgumentException("Receive file error!");
            var idx = 0L;
            var fst = new FileStream(path, FileMode.CreateNew);

            try
            {
                while (idx < length)
                {
                    var sub = (int)Math.Min(length - idx, Links.Buffer);
                    var buf = await socket._ReceiveAsync(sub);
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
    }
}

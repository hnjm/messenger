using System;
using System.Net.Sockets;

namespace Messenger.Foundation.Extensions
{
    public static partial class Extension
    {
        /// <summary>
        /// 先读取数据长度 然后接收该数据 (阻塞模式)
        /// </summary>
        /// <param name="socket">待读取的套接字</param>
        /// <exception cref="SocketException"></exception>
        public static byte[] ReceiveExt(this Socket socket)
        {
            var buf = new byte[sizeof(int)];
            ReceiveExt(socket, buf, 0, buf.Length);
            var str = new byte[BitConverter.ToInt32(buf, 0)];
            ReceiveExt(socket, str, 0, str.Length);
            return str;
        }

        /// <summary>
        /// 从一个套接字读取定长数据 并将其写入到指定的字节数组中 (阻塞模式)
        /// </summary>
        /// <param name="socket">待读取的套接字</param>
        /// <param name="stream">目标字节数组</param>
        /// <param name="index">目标字节数组写入起始位置</param>
        /// <param name="length">待读取的数据长度</param>
        /// <exception cref="SocketException"></exception>
        public static void ReceiveExt(this Socket socket, byte[] stream, int index, int length)
        {
            var sub = length;
            while (true)
            {
                if (sub < 1)
                    break;
                var len = socket.Receive(stream, length + index - sub, sub, SocketFlags.None, out var err);
                if (err != SocketError.Success)
                    throw new SocketException((int)err);
                if (len < 1)
                    throw new SocketException((int)SocketError.ConnectionReset);
                sub -= len;
            }
        }

        /// <summary>
        /// 先发送数据长度 然后发送该数据 (阻塞模式)
        /// </summary>
        public static void SendExt(this Socket socket, byte[] values)
        {
            socket.Send(BitConverter.GetBytes(values.Length));
            socket.Send(values);
        }

        /// <summary>
        /// 设置 TCP 套接字空闲超时
        /// </summary>
        /// <param name="socket">目标套接字</param>
        /// <param name="enable">是否启用该功能</param>
        /// <param name="before">开始确认之前的等待时间</param>
        /// <param name="interval">每次确认等待间隔</param>
        public static int SetKeepAlive(this Socket socket, bool enable, uint before, uint interval)
        {
            if (enable == true && (before < 1 || interval < 1))
                throw new ArgumentException();
            var len = sizeof(uint);
            var val = new byte[len];
            var buf = new byte[len * 3];
            if (enable == true)
            {
                Array.Copy(BitConverter.GetBytes(1U), 0, buf, 0, len);
                Array.Copy(BitConverter.GetBytes(before), 0, buf, len, len);
                Array.Copy(BitConverter.GetBytes(interval), 0, buf, len * 2, len);
            }
            socket.IOControl(IOControlCode.KeepAliveValues, buf, val);
            return BitConverter.ToInt32(val, 0);
        }
    }
}

using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using static System.BitConverter;

namespace Mikodev.Network
{
    public static class LinkExtends
    {
        public static byte[] _Merge(params byte[][] arrays)
        {
            var sum = 0;
            for (int i = 0; i < arrays.Length; i++)
                sum += arrays[i].Length;
            var arr = new byte[sum];
            var idx = 0;
            for (int i = 0; i < arrays.Length; i++)
            {
                var cur = arrays[i];
                var len = cur.Length;
                Buffer.BlockCopy(cur, 0, arr, idx, len);
                idx += len;
            }
            return arr;
        }

        public static Task<Socket> _AcceptAsync(this Socket socket) => Task.Factory.FromAsync(socket.BeginAccept, socket.EndAccept, null);

        public static int _SetKeepAlive(this Socket socket, bool enable = true, uint before = Links.KeepAliveBefore, uint interval = Links.KeepAliveInterval)
        {
            if (enable == true && (before < 1 || interval < 1))
                throw new ArgumentOutOfRangeException("Keep alive argument out of range.");
            var val = new byte[sizeof(uint)];
            var res = _Merge(GetBytes(1U), GetBytes(before), GetBytes(interval));
            socket.IOControl(IOControlCode.KeepAliveValues, res, val);
            return ToInt32(val, 0);
        }

        public static async Task<byte[]> _ReceiveExtendAsync(this Socket socket)
        {
            var buf = await _ReceiveAsync(socket, sizeof(int));
            var len = ToInt32(buf, 0);
            var res = await _ReceiveAsync(socket, len);
            return res;
        }

        public static async Task<byte[]> _ReceiveAsync(this Socket socket, int length)
        {
            if (length < 1 || length > Links.BufferLimit)
                throw new LinkException(LinkError.Overflow, "Buffer length out of range!");
            var buf = new byte[length];
            var idx = 0;
            while (idx < length)
            {
                var sub = length - idx;
                var len = await Task.Factory.FromAsync((a, s) => socket.BeginReceive(buf, idx, sub, SocketFlags.None, a, s), socket.EndReceive, null);
                if (len < 1)
                    throw new SocketException((int)SocketError.ConnectionReset);
                idx += len;
            }
            return buf;
        }

        public static async Task _SendExtendAsync(this Socket socket, byte[] buffer)
        {
            var len = GetBytes(buffer.Length);
            await _SendAsync(socket, len);
            await _SendAsync(socket, buffer);
        }

        public static async Task _SendAsync(this Socket socket, byte[] buffer)
        {
            var idx = 0;
            while (idx < buffer.Length)
            {
                var sub = buffer.Length - idx;
                var len = await Task.Factory.FromAsync((a, o) => socket.BeginSend(buffer, idx, sub, SocketFlags.None, a, o), socket.EndSend, null);
                idx += sub;
            }
        }

        public static LinkPacket _Load(this LinkPacket src, byte[] buf)
        {
            var ori = new PacketReader(buf);
            src._buf = buf;
            src._ori = ori;
            src._src = ori["source"].Pull<int>();
            src._tar = ori["target"].Pull<int>();
            src._pth = ori["path"].Pull<string>();
            src._dat = ori["data", true];
            return src;
        }
    }
}

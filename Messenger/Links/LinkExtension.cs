using Mikodev.Binary;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using static System.BitConverter;

namespace Mikodev.Network
{
    public static class LinkExtension
    {
        public static Generator Generator { get; } = new Generator();

        public static Task ConnectAsyncEx(this Socket socket, EndPoint endpoint) => Task.Factory.FromAsync((arg, obj) => socket.BeginConnect(endpoint, arg, obj), socket.EndConnect, null);

        public static Task<Socket> AcceptAsyncEx(this Socket socket) => Task.Factory.FromAsync(socket.BeginAccept, socket.EndAccept, null);

        public static int SetKeepAlive(this Socket socket, bool enable = true, uint before = Links.KeepAliveBefore, uint interval = Links.KeepAliveInterval)
        {
            if (enable == true && (before < 1 || interval < 1))
                throw new ArgumentOutOfRangeException("Keep alive argument out of range.");
            var result = new byte[sizeof(uint)];
            // struct layout: uint32, uint32, uint32 (total 12 bytes)
            var option = Generator.ToBytes((1U, before, interval));
            _ = socket.IOControl(IOControlCode.KeepAliveValues, option, result);
            return ToInt32(result, 0);
        }

        public static async Task<byte[]> ReceiveAsyncExt(this Socket socket)
        {
            var buf = await ReceiveAsyncEx(socket, sizeof(int));
            var len = ToInt32(buf, 0);
            var res = await ReceiveAsyncEx(socket, len);
            return res;
        }

        public static async Task<byte[]> ReceiveAsyncEx(this Socket socket, int length)
        {
            if (length < 1 || length > Links.BufferLengthLimit)
                throw new LinkException(LinkError.Overflow);
            var offset = 0;
            var buffer = new byte[length];
            while (length > 0)
            {
                var res = await Task.Factory.FromAsync((a, s) => socket.BeginReceive(buffer, offset, length, SocketFlags.None, a, s), socket.EndReceive, null);
                if (res < 1)
                    throw new SocketException((int)SocketError.ConnectionReset);
                offset += res;
                length -= res;
            }
            return buffer;
        }

        public static async Task SendAsyncExt(this Socket socket, byte[] buffer)
        {
            var len = GetBytes(buffer.Length);
            await SendAsyncEx(socket, len);
            await SendAsyncEx(socket, buffer);
        }

        public static Task SendAsyncEx(this Socket socket, byte[] buffer) => SendAsyncEx(socket, buffer, 0, buffer.Length);

        public static async Task SendAsyncEx(this Socket socket, byte[] buffer, int offset, int length)
        {
            while (length > 0)
            {
                var res = await Task.Factory.FromAsync((a, o) => socket.BeginSend(buffer, offset, length, SocketFlags.None, a, o), socket.EndSend, null);
                if (res < 1)
                    throw new SocketException((int)SocketError.ConnectionReset);
                offset += res;
                length -= res;
            }
        }

        public static LinkPacket LoadValue(this LinkPacket me, byte[] buffer)
        {
            var origin = Generator.AsToken(buffer);
            me.Buffer = buffer;
            me.Origin = origin;
            me.Source = origin["source"].As<int>();
            me.Target = origin["target"].As<int>();
            me.Path = origin["path"].As<string>();
            if (((IReadOnlyDictionary<string, Token>)origin).TryGetValue("data", out var data))
                me.Data = data;
            return me;
        }

        public static async Task TimeoutAfter(this Task task, string message = null, int milliseconds = Links.Timeout)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));
            if (milliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(milliseconds));
            using (var src = new CancellationTokenSource())
            {
                var res = await Task.WhenAny(task, Task.Delay(milliseconds));
                if (res != task)
                    throw (string.IsNullOrEmpty(message))
                        ? new TimeoutException()
                        : new TimeoutException(message);
                src.Cancel();
                await task;
            }
        }

        public static async Task<T> TimeoutAfter<T>(this Task<T> task, string message = null, int milliseconds = Links.Timeout)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));
            if (milliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(milliseconds));
            using (var src = new CancellationTokenSource())
            {
                var res = await Task.WhenAny(task, Task.Delay(milliseconds));
                if (res != task)
                    throw (string.IsNullOrEmpty(message))
                        ? new TimeoutException()
                        : new TimeoutException(message);
                src.Cancel();
                return await task;
            }
        }

        public static void AssertFatal(this bool result, string message)
        {
            if (result)
                return;
            Environment.FailFast(message);
        }

        public static void AssertFatal(this bool result, bool assert, string message)
        {
            if (result && assert)
                return;
            Environment.FailFast(message);
        }

        public static void AssertError(this LinkError error)
        {
            if (error == LinkError.Success)
                return;
            throw new LinkException(error);
        }
    }
}

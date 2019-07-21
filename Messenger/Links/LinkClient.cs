using Mikodev.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Mikodev.Network
{
    public sealed class LinkClient : IDisposable
    {
        internal readonly int _id = 0;

        internal readonly object _locker = new object();

        internal readonly Socket _socket = null;

        internal readonly Socket _listen = null;

        internal readonly IPEndPoint _inner = null;

        internal readonly IPEndPoint _outer = null;

        internal readonly IPEndPoint _connected = null;

        internal readonly Queue<byte[]> _msgs = new Queue<byte[]>();

        internal readonly CancellationTokenSource _cancel = new CancellationTokenSource();

        internal readonly Func<Socket, LinkPacket, Task> _requested;

        internal readonly AesManaged _aes = new AesManaged();

        internal bool _started = false;

        internal bool _disposed = false;

        internal long _msglen = 0;

        public int Id => _id;

        public bool IsRunning { get { lock (_locker) { return _started == true && _disposed == false; } } }

        /// <summary>
        /// 本机端点 (不会返回 null)
        /// </summary>
        public IPEndPoint InnerEndPoint => _inner;

        /// <summary>
        /// 服务器报告的相对于服务器的外部端点 (不会返回 null)
        /// </summary>
        public IPEndPoint OuterEndPoint => _outer;

        public event EventHandler<LinkEventArgs<LinkPacket>> Received = null;

        public event EventHandler<LinkEventArgs<Exception>> Disposed = null;

        internal LinkClient(int id, Socket socket, IPEndPoint inner, IPEndPoint outer, byte[] key, byte[] block)
        {
            _id = id;
            _socket = socket;

            _inner = inner;
            _outer = outer;

            _aes.Key = key;
            _aes.IV = block;
        }

        internal LinkClient(int id, Socket socket, Socket listen, IPEndPoint connected, IPEndPoint inner, IPEndPoint outer, byte[] key, byte[] block, Func<Socket, LinkPacket, Task> request)
        {
            _id = id;
            _socket = socket;
            _listen = listen;

            _connected = connected;
            _inner = inner;
            _outer = outer;

            _aes.Key = key;
            _aes.IV = block;
            _requested = request;
        }

        public static async Task<LinkClient> Connect(int id, IPEndPoint target, Func<Socket, LinkPacket, Task> request)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            var listen = new Socket(SocketType.Stream, ProtocolType.Tcp);
            var rsa = RSA.Create();
            var parameters = rsa.ExportParameters(false);

            try
            {
                await socket.ConnectAsyncEx(target).TimeoutAfter("Timeout, at connect to server.");
                _ = socket.SetKeepAlive();
                var local = (IPEndPoint)socket.LocalEndPoint;

                listen.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                listen.Bind(local);
                listen.Listen(Links.ClientSocketLimit);

                var req = LinkExtension.Generator.ToBytes(new
                {
                    source = id,
                    endpoint = local,
                    path = "link.connect",
                    protocol = Links.Protocol,
                    rsa = new
                    {
                        modulus = parameters.Modulus,
                        exponent = parameters.Exponent,
                    }
                });

                await socket.SendAsyncExt(req).TimeoutAfter("Timeout, at client request.");
                var rec = await socket.ReceiveAsyncExt().TimeoutAfter("Timeout, at client response.");

                var rea = LinkExtension.Generator.AsToken(rec);
                rea["result"].As<LinkError>().AssertError();
                var remote = rea["endpoint"].As<IPEndPoint>();
                var rsaKey = rsa.Decrypt(rea["aes"]["key"].As<byte[]>(), RSAEncryptionPadding.OaepSHA1);
                var rasIV = rsa.Decrypt(rea["aes"]["iv"].As<byte[]>(), RSAEncryptionPadding.OaepSHA1);
                return new LinkClient(id, socket, listen, target, local, remote, rsaKey, rasIV, request);
            }
            catch (Exception)
            {
                socket.Dispose();
                listen.Dispose();
                throw;
            }
        }

        public async Task Start()
        {
            var arr = default(Task[]);
            var err = default(Exception);

            lock (_locker)
            {
                if (_started || _disposed)
                    throw new InvalidOperationException("Client has benn marked as started or disposed!");
                _started = true;

                var lst = new[]
                {
                    _listen == null ? null : Task.Run(Listener),
                    Task.Run(Sender),
                    Task.Run(Receiver),
                };
                arr = lst.Where(r => r != null).ToArray();
            }

            try
            {
                await await Task.WhenAny(arr);
            }
            catch (Exception ex)
            {
                if ((ex is SocketException || ex is ObjectDisposedException) == false)
                    Log.Error(ex);
                err = ex;
            }

            Dispose(err);
            await Task.WhenAll(arr);
        }

        public void Enqueue(byte[] buffer)
        {
            var len = buffer?.Length ?? throw new ArgumentNullException(nameof(buffer));
            if (len < 1 || len > Links.BufferLengthLimit)
                throw new ArgumentOutOfRangeException(nameof(buffer));
            lock (_locker)
            {
                if (_disposed)
                    return;
                _msglen += len;
                _msgs.Enqueue(buffer);
            }
        }

        internal async Task Sender()
        {
            bool _Dequeue(out byte[] buf)
            {
                lock (_locker)
                {
                    if (_msglen > Links.BufferQueueLimit)
                        throw new LinkException(LinkError.QueueLimited);
                    if (_msglen > 0)
                    {
                        buf = _msgs.Dequeue();
                        _msglen -= buf.Length;
                        return true;
                    }
                }

                buf = null;
                return false;
            }

            while (_cancel.IsCancellationRequested == false)
            {
                if (_Dequeue(out var buf))
                    await _socket.SendAsyncExt(_aes.Encrypt(buf));
                else
                    await Task.Delay(Links.Delay);
                continue;
            }
        }

        internal async Task Receiver()
        {
            while (_cancel.IsCancellationRequested == false)
            {
                var buf = await _socket.ReceiveAsyncExt();

                var rec = Received;
                if (rec == null)
                    continue;
                var res = _aes.Decrypt(buf);
                var pkt = new LinkPacket().LoadValue(res);
                var arg = new LinkEventArgs<LinkPacket>(pkt);
                rec.Invoke(this, arg);
            }
        }

        internal async Task Listener()
        {
            while (_cancel.IsCancellationRequested == false)
            {
                var accept = await _listen.AcceptAsyncEx();

                _ = Task.Run(async () =>
                {
                    try
                    {
                        _ = accept.SetKeepAlive();
                        var buffer = await accept.ReceiveAsyncExt().TimeoutAfter("Timeout, at receive header packet.");
                        var packet = new LinkPacket().LoadValue(buffer);
                        await _requested.Invoke(accept, packet);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex);
                    }
                    finally
                    {
                        accept.Dispose();
                    }
                });
            }
        }

        internal void Dispose(Exception error)
        {
            lock (_locker)
            {
                if (_disposed)
                    return;
                _disposed = true;
                _msgs.Clear();
                _msglen = 0;
            }
            _cancel.Cancel();
            _cancel.Dispose();
            _socket.Dispose();
            _listen?.Dispose();
            _aes.Dispose();

            var handler = Disposed;
            if (handler == null)
                return;
            _ = Task.Run(() =>
            {
                if (error == null)
                    error = new OperationCanceledException("Client disposed manually or by GC.");
                var args = new LinkEventArgs<Exception>(error);
                handler.Invoke(this, args);
            });
        }

        public void Dispose() => Dispose(null);
    }
}

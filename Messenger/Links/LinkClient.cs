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
        internal readonly object _loc = new object();
        internal readonly Socket _socket = null;
        internal readonly Socket _listen = null;
        internal readonly IPEndPoint _inner = null;
        internal readonly IPEndPoint _outer = null;
        internal readonly IPEndPoint _connected = null;
        internal readonly Queue<byte[]> _msgs = new Queue<byte[]>();
        internal readonly CancellationTokenSource _cancel = new CancellationTokenSource();
        internal readonly Action<Socket, LinkPacket> _requested;
        internal readonly byte[] _key = null;
        internal readonly byte[] _blk = null;

        internal bool _started = false;
        internal bool _disposed = false;
        internal long _msglen = 0;

        public int Id => _id;

        public bool IsRunning { get { lock (_loc) { return _started == true && _disposed == false; } } }

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

            _key = key;
            _blk = block;
        }

        internal LinkClient(int id, Socket socket, Socket listen, IPEndPoint connected, IPEndPoint inner, IPEndPoint outer, byte[] key, byte[] block, Action<Socket, LinkPacket> request)
        {
            _id = id;
            _socket = socket;
            _listen = listen;

            _connected = connected;
            _inner = inner;
            _outer = outer;

            _key = key;
            _blk = block;
            _requested = request;
        }

        public static async Task<LinkClient> Connect(int id, IPEndPoint target, Action<Socket, LinkPacket> request)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var soc = new Socket(SocketType.Stream, ProtocolType.Tcp);
            var lis = new Socket(SocketType.Stream, ProtocolType.Tcp);
            var rsa = RSA.Create();
            var par = rsa.ExportParameters(false);
            var iep = default(IPEndPoint);
            var oep = default(IPEndPoint);
            var key = default(byte[]);
            var blk = default(byte[]);

            try
            {
                await soc.ConnectAsyncEx(target).TimeoutAfter("Timeout, at connect to server.");
                soc.SetKeepAlive();
                iep = (IPEndPoint)soc.LocalEndPoint;

                lis.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                lis.Bind(iep);
                lis.Listen(Links.ClientSocketLimit);

                var req = PacketWriter.Serialize(new
                {
                    source = id,
                    endpoint = iep,
                    path = "link.connect",
                    protocol = Links.Protocol,
                    rsa = new
                    {
                        modulus = par.Modulus,
                        exponent = par.Exponent,
                    }
                });

                await soc.SendAsyncExt(req.GetBytes()).TimeoutAfter("Timeout, at client request.");
                var rec = await soc.ReceiveAsyncExt().TimeoutAfter("Timeout, at client response.");

                var rea = new PacketReader(rec);
                rea["result"].GetValue<LinkError>().AssertError();
                oep = rea["endpoint"].GetValue<IPEndPoint>();
                key = rsa.Decrypt(rea["aes/key"].GetArray<byte>(), RSAEncryptionPadding.OaepSHA1);
                blk = rsa.Decrypt(rea["aes/iv"].GetArray<byte>(), RSAEncryptionPadding.OaepSHA1);
            }
            catch (Exception)
            {
                soc.Dispose();
                lis.Dispose();
                throw;
            }

            return new LinkClient(id, soc, lis, target, iep, oep, key, blk, request);
        }

        public async Task Start()
        {
            var arr = default(Task[]);
            var err = default(Exception);

            lock (_loc)
            {
                if (_started || _disposed)
                    throw new InvalidOperationException("Client has benn marked as started or disposed!");
                _started = true;

                var lst = new[]
                {
                    _listen == null ? null : Task.Run(_Listener),
                    Task.Run(_Sender),
                    Task.Run(_Receiver),
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

            _Dispose(err);
            await Task.WhenAll(arr);
        }

        public void Enqueue(byte[] buffer)
        {
            var len = buffer?.Length ?? throw new ArgumentNullException(nameof(buffer));
            if (len < 1 || len > Links.BufferLengthLimit)
                throw new ArgumentOutOfRangeException(nameof(buffer));
            lock (_loc)
            {
                if (_disposed)
                    return;
                _msglen += len;
                _msgs.Enqueue(buffer);
            }
        }

        internal async Task _Sender()
        {
            bool _Dequeue(out byte[] buf)
            {
                lock (_loc)
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
                    await _socket.SendAsyncExt(LinkCrypto.Encrypt(buf, _key, _blk));
                else
                    await Task.Delay(Links.Delay);
                continue;
            }
        }

        internal async Task _Receiver()
        {
            while (_cancel.IsCancellationRequested == false)
            {
                var buf = await _socket.ReceiveAsyncExt();

                var rec = Received;
                if (rec == null)
                    continue;
                var res = LinkCrypto.Decrypt(buf, _key, _blk);
                var pkt = new LinkPacket().LoadValue(res);
                var arg = new LinkEventArgs<LinkPacket>(pkt);
                rec.Invoke(this, arg);
            }
        }

        internal async Task _Listener()
        {
            while (_cancel.IsCancellationRequested == false)
            {
                var soc = await _listen.AcceptAsyncEx();

                Task.Run(async () =>
                {
                    try
                    {
                        soc.SetKeepAlive();
                        var buf = await soc.ReceiveAsyncExt().TimeoutAfter("Timeout, at receive header packet.");
                        var pkt = new LinkPacket().LoadValue(buf);
                        _requested.Invoke(soc, pkt);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex);
                    }
                    finally
                    {
                        soc.Dispose();
                    }
                }).Ignore();
            }
        }

        internal void _Dispose(Exception err = null)
        {
            lock (_loc)
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

            var dis = Disposed;
            if (dis == null)
                return;
            Task.Run(() =>
            {
                if (err == null)
                    err = new OperationCanceledException("Client disposed manually or by GC.");
                var arg = new LinkEventArgs<Exception>(err);
                dis.Invoke(this, arg);
            });
        }

        public void Dispose() => _Dispose();
    }
}

using Mikodev.Logger;
using System;
using System.Collections.Generic;
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
        internal readonly Queue<byte[]> _msgs = new Queue<byte[]>();
        internal readonly CancellationTokenSource _cancel = new CancellationTokenSource();

        internal bool _started = false;
        internal bool _disposed = false;
        internal long _msglen = 0;
        internal byte[] _key = null;
        internal byte[] _blk = null;

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

        public event EventHandler<LinkEventArgs<Socket>> Requested = null;

        internal LinkClient(int id, Socket socket, IPEndPoint inner, IPEndPoint outer)
        {
            _id = id;
            _socket = socket ?? throw new ArgumentNullException(nameof(socket));
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _outer = outer ?? throw new ArgumentNullException(nameof(outer));
        }

        public LinkClient(int id, EndPoint server)
        {
            var soc = new Socket(SocketType.Stream, ProtocolType.Tcp);
            var lis = new Socket(SocketType.Stream, ProtocolType.Tcp);
            var rsa = new RSACryptoServiceProvider();
            var iep = default(IPEndPoint);
            var oep = default(IPEndPoint);
            var key = default(byte[]);
            var blk = default(byte[]);

            try
            {
                soc.ConnectAsyncEx(server).WaitTimeout("Timeout when connect to server.");
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
                    rsakey = rsa.ToXmlString(false),
                });
                soc.SendAsyncExt(req.GetBytes()).WaitTimeout("Timeout when client request.");

                var rec = soc.ReceiveAsyncExt().WaitTimeout("Timeout when client response.");
                var rea = new PacketReader(rec);
                rea["result"].Pull<LinkError>().AssertError();

                oep = rea["endpoint"].Pull<IPEndPoint>();
                key = rsa.Decrypt(rea["aeskey"].PullList(), true);
                blk = rsa.Decrypt(rea["aesiv"].PullList(), true);
            }
            catch (Exception)
            {
                soc.Dispose();
                lis.Dispose();
                throw;
            }

            _id = id;
            _socket = soc;
            _listen = lis;
            _inner = iep;
            _outer = oep;
            _key = key;
            _blk = blk;
        }

        public void Start()
        {
            lock (_loc)
            {
                if (_started || _disposed)
                    throw new InvalidOperationException("Client has benn marked as started or disposed!");
                _started = true;
                if (_listen != null)
                    _Listener().ContinueWith(_Clean);
                _Sender().ContinueWith(_Clean);
                _Receiver().ContinueWith(_Clean);
            }
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

            while (true)
            {
                if (_cancel.IsCancellationRequested)
                    throw new TaskCanceledException("Client sender task exited.");
                if (_Dequeue(out var buf))
                    await _socket.SendAsyncExt(LinkCrypto.Encrypt(buf, _key, _blk));
                else
                    await Task.Delay(Links.Delay);
                continue;
            }
        }

        internal async Task _Receiver()
        {
            while (true)
            {
                if (_cancel.IsCancellationRequested)
                    throw new TaskCanceledException("Client receiver task exited.");
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
            void _Invoke(Socket socket)
            {
                Task.Run(() =>
                {
                    Requested?.Invoke(this, new LinkEventArgs<Socket>(socket));
                })
                .ContinueWith(task =>
                {
                    Log.Error(task.Exception);
                    socket.Dispose();
                });
            }

            while (true)
            {
                if (_cancel.IsCancellationRequested)
                    throw new TaskCanceledException("Client listener task exited.");
                var soc = default(Socket);

                try
                {
                    soc = await _listen.AcceptAsyncEx();
                    soc.SetKeepAlive();
                    _Invoke(soc);
                }
                catch (SocketException ex)
                {
                    Log.Error(ex);
                    soc?.Dispose();
                    continue;
                }
            }
        }

        internal void _Clean(Task task)
        {
            var err = task.Exception;
            Log.Error(err);
            _OnDispose(err);
        }

        internal void _OnDispose(Exception err = null)
        {
            lock (_loc)
            {
                if (_disposed)
                    return;
                _disposed = true;
            }
            _cancel.Cancel();
            _socket.Dispose();
            _listen?.Dispose();

            var dis = Disposed;
            if (dis == null)
                return;
            Task.Run(() =>
            {
                if (err == null)
                    err = new TaskCanceledException("Client disposed manually or by GC.");
                var arg = new LinkEventArgs<Exception>(err);
                dis.Invoke(this, arg);
            });
        }

        public void Dispose() => _OnDispose();
    }
}

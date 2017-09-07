using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Mikodev.Network
{
    public sealed class LinkClient : IDisposable
    {
        internal bool _started = false;
        internal bool _disposed = false;
        internal long _msglen = 0;
        internal readonly int _id = 0;
        internal readonly object _loc = new object();
        internal AesManaged _aes = null;
        internal Exception _except = null;
        internal IPEndPoint _iep = null;
        internal readonly Queue<byte[]> _msgs = new Queue<byte[]>();
        internal Socket _socket = null;

        public int ID => _id;

        public bool IsRunning => _disposed == false && _started == true;

        public Exception Exception => _except;

        public IPEndPoint InnerEndPoint => _socket?.LocalEndPoint as IPEndPoint;

        public IPEndPoint OuterEndPoint => _iep;

        public event EventHandler<LinkEventArgs<LinkPacket>> Received = null;

        public event EventHandler Shutdown = null;

        public LinkClient(int id) => _id = id;

        public void Start(Socket socket)
        {
            lock (_loc)
            {
                if (_started || _disposed)
                    throw new InvalidOperationException("Client has benn marked as started or disposed!");
                _started = true;
                _socket = socket;
                _Sender().ContinueWith(t => _Shutdown(t.Exception));
                _Receiver().ContinueWith(t => _Shutdown(t.Exception));
            }
        }

        public void Start(IPEndPoint ep)
        {
            lock (_loc)
            {
                if (_started || _disposed)
                    throw new InvalidOperationException("Client has benn marked as started or disposed!");
                _started = true;
            }

            var soc = new Socket(SocketType.Stream, ProtocolType.Tcp);
            var rsa = new RSACryptoServiceProvider();
            var buf = default(byte[]);
            var req = PacketWriter.Serialize(new
            {
                id = _id,
                protocol = Links.Protocol,
                rsakey = rsa.ToXmlString(false),
            });

            try
            {
                if (Task.Run(() => soc.Connect(ep)).Wait(Links.Timeout) == false)
                    throw new TimeoutException("Timeout when connect to server.");
                soc._SetKeepAlive();
                if (soc._SendExtendAsync(req.GetBytes()).Wait(Links.Timeout) == false)
                    throw new TimeoutException("Timeout when client request.");
                if (Task.Run(async () => buf = await soc._ReceiveExtendAsync()).Wait(Links.Timeout) == false)
                    throw new TimeoutException("Timeout when client response.");

                var rea = new PacketReader(buf);
                var err = rea["result"].Pull<LinkError>();
                if (err != LinkError.Success)
                    throw new LinkException(err);
                var aeskey = rea["aeskey"].PullList();
                var aesiv = rea["aesiv"].PullList();
                _iep = rea["endpoint"].Pull<IPEndPoint>();
                _aes = new AesManaged() { Key = rsa.Decrypt(aeskey, true), IV = rsa.Decrypt(aesiv, true) };
                lock (_loc)
                {
                    if (_disposed)
                        throw new InvalidOperationException("Client has benn marked as disposed!");
                    _socket = soc;
                }
            }
            catch (Exception)
            {
                soc.Dispose();
                throw;
            }

            _Sender().ContinueWith(t => _Shutdown(t.Exception));
            _Receiver().ContinueWith(t => _Shutdown(t.Exception));
        }

        public void Enqueue(byte[] buffer)
        {
            var len = buffer?.Length ?? throw new ArgumentNullException(nameof(buffer));
            if (len < 1 || len > Links.BufferLimit)
                throw new ArgumentOutOfRangeException(nameof(buffer));
            lock (_loc)
            {
                if (_disposed || _msglen > Links.Queue)
                    return;
                _msglen += len;
                _msgs.Enqueue(buffer);
            }
        }

        internal async Task _Sender()
        {
            bool dequeue(out byte[] buf)
            {
                lock (_loc)
                {
                    if (_msglen > Links.Queue)
                        throw new LinkException(LinkError.QueueLimited, "Message queue length out of range!");
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

            while (_socket != null)
            {
                if (dequeue(out var buf))
                    await _socket._SendExtendAsync(_aes._Encrypt(buf));
                else
                    await Task.Delay(Links.Delay);
            }
        }

        internal async Task _Receiver()
        {
            while (_socket != null)
            {
                var buf = await _socket._ReceiveExtendAsync();
                var res = _aes._Decrypt(buf);
                _Received(new LinkPacket()._Load(res));
            }
        }

        internal int _Received(LinkPacket packet)
        {
            if (packet.Source == Links.ID && packet.Path == "link.shutdown")
                return _Shutdown();
            Received?.Invoke(this, new LinkEventArgs<LinkPacket>() { Source = this, Record = packet });
            return 0;
        }

        internal int _Shutdown(object obj = null)
        {
            if (obj != null)
                Trace.WriteLine(obj);
            lock (_loc)
                if (_disposed)
                    return 0;
            if (obj is Exception ex)
                _except = ex;
            Dispose();
            Shutdown?.Invoke(this, new EventArgs());
            return 0;
        }

        public void Dispose()
        {
            lock (_loc)
            {
                if (_disposed)
                    return;
                _disposed = true;
                _socket?.Dispose();
                _socket = null;
            }
        }
    }
}

﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Mikodev.Network
{
    public sealed class LinkClient
    {
        internal bool _started = false;
        internal bool _disposed = false;
        internal readonly int _id = 0;
        internal readonly object _loc = new object();
        internal AesManaged _aes = null;
        internal Exception _except = null;
        internal IPEndPoint _iep = null;

        internal readonly ConcurrentQueue<byte[]> _msgs = new ConcurrentQueue<byte[]>();
        internal Socket _socket = null;
        internal Socket _trans = null;

        public int ID => _id;

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
                    throw new InvalidOperationException();
                _started = true;
                _socket = socket;
                Task.Run(async () => await _Sender()).ContinueWith(t => _Close(t.Exception));
                Task.Run(async () => await _Receiver()).ContinueWith(t => _Close(t.Exception));
            }
        }

        public void Start(IPEndPoint ep)
        {
            lock (_loc)
            {
                if (_started || _disposed)
                    throw new InvalidOperationException();
                _started = true;
            }

            var rsa = new RSACryptoServiceProvider();
            var buf = default(byte[]);
            var req = PacketWriter.Serialize(new
            {
                id = _id,
                protocol = Links.Protocol,
                rsakey = rsa.ToXmlString(false),
            });

            var soc = new Socket(SocketType.Stream, ProtocolType.Tcp);
            var tra = new Socket(SocketType.Stream, ProtocolType.Tcp);
            void close()
            {
                soc?.Dispose();
                soc = null;
                tra?.Dispose();
                tra = null;
            }

            try
            {
                if (Task.Run(() => soc.Connect(ep)).Wait(Links.Timeout) == false)
                    throw new TimeoutException("Timeout when connect to server.");
                soc._SetKeepAlive();
                if (Task.Run(async () => await soc._SendExtendAsync(req.GetBytes())).Wait(Links.Timeout) == false)
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

                tra.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                tra.Bind(soc.LocalEndPoint);
                tra.Listen(Links.Count);
            }
            catch (Exception)
            {
                close();
                throw;
            }

            lock (_loc)
            {
                if (_disposed)
                {
                    close();
                    throw new InvalidOperationException();
                }

                _socket = soc;
                _trans = tra;

                Task.Run(async () => await _Sender()).ContinueWith(t => _Close(t.Exception));
                Task.Run(async () => await _Receiver()).ContinueWith(t => _Close(t.Exception));
            }
        }

        public void Enqueue(byte[] buffer)
        {
            if (buffer == null)
                throw new LinkException(LinkError.AssertFailed);
            _msgs.Enqueue(buffer);
        }

        public void Enqueue(object sender, LinkEventArgs<LinkPacket> e)
        {
            var rcd = e.Record;
            if (rcd.Source == _id)
                return;
            _msgs.Enqueue(rcd.Buffer);
        }

        internal async Task _Sender()
        {
            while (_socket != null)
            {
                if (_msgs.TryDequeue(out var buf))
                {
                    var res = _aes._Encrypt(buf);
                    await _socket._SendExtendAsync(res);
                    continue;
                }

                var len = _msgs.Sum(r => r.Length);
                if (len > Links.Queue)
                    throw new LinkException(LinkError.Overflow);
                await Task.Delay(1);
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

        internal void _Received(LinkPacket packet)
        {
            if (packet.Source == Links.ID && string.Equals(packet.Path, "link.shutdown"))
                _Close();
            else
                Received?.Invoke(this, new LinkEventArgs<LinkPacket>() { Source = this, Record = packet });
        }

        internal void _Close(object ex = null)
        {
            if (ex != null)
                Trace.WriteLine(ex);
            lock (_loc)
            {
                if (_disposed == true)
                    return;
                _disposed = true;
                if (ex is Exception exc)
                    _except = exc;
                _socket?.Dispose();
                _socket = null;
                _trans?.Dispose();
                _trans = null;
            }
            Shutdown?.Invoke(this, new EventArgs());
        }

        public void Dispose() => _Close();
    }
}
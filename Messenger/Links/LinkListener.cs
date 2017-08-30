using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Mikodev.Network
{
    public sealed partial class LinkListener
    {
        internal int _climit = Links.Count;

        internal int _port = Links.Port;

        internal Socket _soc = null;

        internal Dictionary<int, LinkClient> _dic = new Dictionary<int, LinkClient>();

        internal Dictionary<int, List<int>> _gro = new Dictionary<int, List<int>>();

        public Task Listen(int port = Links.Port, int count = Links.Count)
        {
            if (count < 1)
                throw new ArgumentOutOfRangeException(nameof(count), "The count limit must bigger than zero!");

            var iep = new IPEndPoint(IPAddress.Any, port);
            var soc = new Socket(SocketType.Stream, ProtocolType.Tcp);

            try
            {
                soc.Bind(iep);
                soc.Listen(count);
                if (Interlocked.CompareExchange(ref _soc, soc, null) != null)
                    throw new InvalidOperationException();
                _climit = count;
                _port = port;
            }
            catch (Exception)
            {
                soc.Dispose();
                throw;
            }

            return Task.Run(new Action(_Listen));
        }

        private void _Listen()
        {
            while (_soc != null)
            {
                var clt = default(Socket);

                try
                {
                    clt = _soc.Accept();
                    clt._SetKeepAlive();
                }
                catch (SocketException ex)
                {
                    Trace.WriteLine(ex);
                    continue;
                }

                Task.Run(() => _Handle(clt)).ContinueWith(t =>
                {
                    if (t.Exception != null)
                    {
                        Trace.WriteLine(t.Exception);
                        clt.Dispose();
                    }
                });
            }
        }

        private void _Handle(Socket client)
        {
            LinkError check(int code)
            {
                if (code <= Links.ID)
                    return LinkError.CodeInvalid;
                if (_dic.Count >= _climit)
                    return LinkError.CountLimited;
                lock (_dic)
                {
                    if (_dic.ContainsKey(code))
                        return LinkError.CodeConflict;
                    _dic.Add(code, null);
                    return LinkError.Success;
                }
            }

            void remove(int code)
            {
                lock (_dic)
                {
                    if (_dic.TryGetValue(code, out var val) && val == null)
                        _dic.Remove(code);
                    else throw new LinkException(LinkError.AssertFailed);
                }
            }

            var rsa = new RSACryptoServiceProvider();
            var aes = new AesManaged();
            var buf = default(byte[]);
            var err = LinkError.None;
            var cid = 0;

            byte[] respond()
            {
                var rea = new PacketReader(buf);
                if (string.Equals(rea["protocol"].Pull<string>(), Links.Protocol, StringComparison.InvariantCultureIgnoreCase) == false)
                    throw new LinkException(LinkError.ProtocolMismatch);
                cid = rea["id"].Pull<int>();
                err = check(cid);
                rsa.FromXmlString(rea["rsakey"].Pull<string>());
                var res = PacketWriter.Serialize(new
                {
                    result = err,
                    aeskey = rsa.Encrypt(aes.Key, true),
                    aesiv = rsa.Encrypt(aes.IV, true),
                    endpoint = (IPEndPoint)client.RemoteEndPoint
                });
                return res.GetBytes();
            }

            if (Task.Run(async () => buf = await client._ReceiveExtendAsync()).Wait(Links.Timeout) == false)
            {
                throw new TimeoutException("Listener request timeout.");
            }

            try
            {
                if (Task.Run(async () => await client._SendExtendAsync(respond())).Wait(Links.Timeout) == false)
                {
                    throw new TimeoutException("Listener response timeout.");
                }
            }
            catch (Exception)
            {
                if (err == LinkError.Success)
                    remove(cid);
                throw;
            }

            if (err != LinkError.Success)
            {
                throw new LinkException(err);
            }

            var clt = new LinkClient(cid) { _aes = aes };
            clt.Received += _LinkClient_Received;
            clt.Shutdown += _LinkClient_Shutdown;

            try
            {
                Monitor.Enter(_dic);
                Monitor.Enter(_gro);

                remove(cid);
                _dic.Add(cid, clt);
                _gro.Add(cid, new List<int>());
            }
            finally
            {
                Monitor.Exit(_gro);
                Monitor.Exit(_dic);
            }

            clt.Start(client);
        }

        private void _LinkClient_Shutdown(object sender, EventArgs e)
        {
            var clt = (LinkClient)sender;
            var lst = new List<int>();
            var wtr = PacketWriter.Serialize(new
            {
                source = Links.ID,
                target = Links.ID,
                path = "user.list",
            });

            try
            {
                Monitor.Enter(_dic);
                Monitor.Enter(_gro);

                _dic.Remove(clt._id);
                _gro.Remove(clt._id);
                foreach (var c in _dic) lst.Add(c.Key);

                var buf = wtr.PushList("data", lst).GetBytes();
                foreach (var i in _dic) i.Value.Enqueue(buf);
            }
            finally
            {
                Monitor.Exit(_gro);
                Monitor.Exit(_dic);
            }
        }

        private void _LinkClient_Received(object sender, LinkEventArgs<LinkPacket> arg)
        {
            var rea = arg.Record;
            var src = rea.Source;
            var tar = rea.Target;
            var pth = rea.Path;

            if (tar == Links.ID)
            {
                if (pth == "user.groups")
                {
                    var lst = rea.Data.PullList<int>().ToList();
                    lst.RemoveAll(r => r < Links.ID == false);
                    lock (_gro)
                    {
                        _gro[src].Clear();
                        _gro[src] = lst;
                    }
                    return;
                }
                lock (_dic)
                {
                    foreach (var i in _dic)
                    {
                        if (i.Key != src) i.Value.Enqueue(rea.Buffer);
                    }
                }
            }
            else if (tar > Links.ID)
            {
                lock (_dic)
                {
                    _dic[tar].Enqueue(rea.Buffer);
                }
            }
            else
            {
                lock (_gro)
                {
                    foreach (var i in _gro)
                    {
                        if (i.Key != src && i.Value.Contains(tar)) _dic[i.Key].Enqueue(rea.Buffer);
                    }
                }
            }
        }
    }
}

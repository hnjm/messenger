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
        internal readonly object _obj = new object();

        internal Socket _soc = null;
        internal readonly Dictionary<int, LinkClient> _dic = new Dictionary<int, LinkClient>();
        internal readonly Dictionary<int, List<int>> _gro = new Dictionary<int, List<int>>();

        public Task Listen(int port = Links.Port, int count = Links.Count)
        {
            if (count < 1)
                throw new ArgumentOutOfRangeException(nameof(count), "Count limit must bigger than zero!");
            var iep = new IPEndPoint(IPAddress.Any, port);
            var soc = new Socket(SocketType.Stream, ProtocolType.Tcp);

            try
            {
                soc.Bind(iep);
                soc.Listen(count);
                if (Interlocked.CompareExchange(ref _soc, soc, null) != null)
                    throw new InvalidOperationException("Listener socket not null!");
                _climit = count;
                _port = port;
            }
            catch (Exception)
            {
                soc.Dispose();
                throw;
            }

            return _Listen();
        }

        private async Task _Listen()
        {
            void _Invoke(Socket soc) => Task.Run(() => _Handle(soc)).ContinueWith(tsk =>
            {
                if (tsk.Exception == null)
                    return;
                Trace.WriteLine(tsk.Exception);
                soc.Dispose();
            });

            while (true)
            {
                try
                {
                    var clt = await _soc._AcceptAsync();
                    _Invoke(clt);
                }
                catch (SocketException ex)
                {
                    Trace.WriteLine(ex);
                }
            }
        }

        private void _Handle(Socket client)
        {
            LinkError _Check(int code)
            {
                if (code <= Links.ID)
                    return LinkError.CodeInvalid;
                if (_dic.Count >= _climit)
                    return LinkError.CountLimited;
                lock (_obj)
                {
                    if (_dic.ContainsKey(code))
                        return LinkError.CodeConflict;
                    _dic.Add(code, null);
                    return LinkError.Success;
                }
            }

            void _Remove(int code)
            {
                lock (_obj)
                {
                    if (_dic.TryGetValue(code, out var val) && val == null)
                        _dic.Remove(code);
                    else throw new LinkException(LinkError.AssertFailed, "Remove code mark error!");
                }
            }

            var aes = new AesManaged();
            var buf = default(byte[]);
            var err = LinkError.None;
            var cid = 0;

            byte[] _Respond()
            {
                var rea = new PacketReader(buf);
                var rsa = new RSACryptoServiceProvider();
                if (string.Equals(rea["protocol"].Pull<string>(), Links.Protocol, StringComparison.InvariantCultureIgnoreCase) == false)
                    throw new LinkException(LinkError.ProtocolMismatch);
                cid = rea["id"].Pull<int>();
                err = _Check(cid);
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

            try
            {
                if (Task.Run(async () => buf = await client._ReceiveExtendAsync()).Wait(Links.Timeout) == false)
                    throw new TimeoutException("Listener request timeout.");
                if (client._SendExtendAsync(_Respond()).Wait(Links.Timeout) == false)
                    throw new TimeoutException("Listener response timeout.");
                if (err != LinkError.Success)
                    throw new LinkException(err);
            }
            catch (Exception)
            {
                if (err == LinkError.Success)
                    _Remove(cid);
                throw;
            }

            var clt = new LinkClient(cid) { _aes = aes };
            clt.Received += _LinkClient_Received;
            clt.Shutdown += _LinkClient_Shutdown;

            lock (_obj)
            {
                _Remove(cid);
                _dic.Add(cid, clt);
                _gro.Add(cid, new List<int>());
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

            lock (_obj)
            {
                _dic.Remove(clt._id);
                _gro.Remove(clt._id);
                foreach (var c in _dic)
                    lst.Add(c.Key);
                var buf = wtr.PushList("data", lst).GetBytes();
                foreach (var i in _dic)
                    i.Value.Enqueue(buf);
            }
        }

        private void _LinkClient_Received(object sender, LinkEventArgs<LinkPacket> arg)
        {
            var rea = arg.Record;
            var src = rea.Source;
            var tar = rea.Target;
            var pth = rea.Path;

            switch (tar)
            {
                case Links.ID when pth == "user.group":
                    var lst = rea.Data.PullList<int>().ToList();
                    if (lst.Count > Links.Group)
                        throw new LinkException(LinkError.GroupLimited, "Group count out of range.");
                    lst.RemoveAll(r => r < Links.ID == false);
                    lock (_obj)
                        _gro[src] = lst;
                    return;

                case Links.ID:
                    lock (_obj)
                        foreach (var i in _dic)
                            if (i.Key != src)
                                i.Value.Enqueue(rea.Buffer);
                    return;

                case int _ when tar > Links.ID:
                    lock (_obj)
                        if (_dic.TryGetValue(tar, out var clt))
                            clt.Enqueue(rea.Buffer);
                    return;

                default:
                    lock (_obj)
                        foreach (var i in _gro)
                            if (i.Key != src && i.Value.Contains(tar))
                                _dic[i.Key].Enqueue(rea.Buffer);
                    return;
            }
        }
    }
}

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
        internal readonly object _loc = new object();

        internal Socket _soc = null;
        internal readonly Dictionary<int, LinkClient> _dic = new Dictionary<int, LinkClient>();
        internal readonly Dictionary<int, HashSet<int>> _gro = new Dictionary<int, HashSet<int>>();
        internal readonly Dictionary<int, HashSet<int>> _set = new Dictionary<int, HashSet<int>>();

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
                lock (_loc)
                {
                    if (_dic.ContainsKey(code))
                        return LinkError.CodeConflict;
                    _dic.Add(code, null);
                    return LinkError.Success;
                }
            }

            void _Remove(int code)
            {
                lock (_loc)
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

            lock (_loc)
            {
                _Remove(cid);
                _dic.Add(cid, clt);
                _gro.Add(cid, new HashSet<int>());
            }

            clt.Start(client);
        }

        private void _ResetGroup(int source, IEnumerable<int> target = null)
        {
            var set = _gro[source];
            foreach (var i in set)
            {
                var gro = _set[i];
                var res = gro.Remove(source);
                if (gro.Count > 0)
                    continue;
                res &= _set.Remove(i);
            }

            if (target == null)
            {
                _gro.Remove(source);
                return;
            }

            set.Clear();
            set.UnionWith(target);
            foreach (var i in target)
            {
                if (_set.TryGetValue(i, out var gro) == false)
                    _set.Add(i, (gro = new HashSet<int>()));
                var res = gro.Add(source);
            }
        }

        private void _LinkClient_Shutdown(object sender, EventArgs e)
        {
            var clt = (LinkClient)sender;
            var cid = clt._id;
            var lst = new List<int>();
            var wtr = PacketWriter.Serialize(new
            {
                source = Links.ID,
                target = Links.ID,
                path = "user.list",
            });

            lock (_loc)
            {
                _ResetGroup(cid);
                _dic.Remove(cid);
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
                    var lst = rea.Data.PullList<int>().Where(r => r < Links.ID);
                    var set = new HashSet<int>(lst);
                    if (set.Count > Links.Group)
                        throw new LinkException(LinkError.GroupLimited, "Group count out of range.");
                    lock (_loc)
                        _ResetGroup(src, set);
                    return;

                case Links.ID:
                    lock (_loc)
                        foreach (var i in _dic)
                            if (i.Key != src)
                                i.Value.Enqueue(rea.Buffer);
                    return;

                case int _ when tar > Links.ID:
                    lock (_loc)
                        if (_dic.TryGetValue(tar, out var clt))
                            clt.Enqueue(rea.Buffer);
                    return;

                default:
                    lock (_loc)
                        if (_set.TryGetValue(tar, out var val))
                            foreach (var i in val)
                                if (i != src)
                                    _dic[i].Enqueue(rea.Buffer);
                    return;
            }
        }
    }
}

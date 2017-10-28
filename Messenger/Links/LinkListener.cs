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
                var ex = tsk.Exception;
                if (ex == null)
                    return;
                Log.Err(ex);
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
                    Log.Err(ex);
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

            var key = LinkCrypto.GetKey();
            var blk = LinkCrypto.GetBlock();
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
                    aeskey = rsa.Encrypt(key, true),
                    aesiv = rsa.Encrypt(blk, true),
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
                    lock (_loc)
                        _dic.Remove(cid);
                throw;
            }

            var clt = new LinkClient(cid) { _key = key, _blk = blk };
            clt.Received += _LinkClient_Received;
            clt.Shutdown += _LinkClient_Shutdown;

            lock (_loc)
            {
                _dic.Remove(cid);
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
                gro.Remove(source);
                if (gro.Count > 0)
                    continue;
                _set.Remove(i);
            }

            if (target == null)
            {
                _gro.Remove(source);
                _dic.Remove(source);
                return;
            }

            set.Clear();
            set.UnionWith(target);
            foreach (var i in target)
            {
                if (_set.TryGetValue(i, out var gro) == false)
                    _set.Add(i, (gro = new HashSet<int>()));
                gro.Add(source);
            }
        }

        private void _LinkClient_Shutdown(object sender, EventArgs e)
        {
            var cid = ((LinkClient)sender)._id;
            var wtr = PacketWriter.Serialize(new
            {
                source = Links.ID,
                target = Links.ID,
                path = "user.list",
            });

            lock (_loc)
            {
                _ResetGroup(cid);
                var lst = _dic.Where(r => r.Value != null).Select(r => r.Key);
                var buf = wtr.PushList("data", lst).GetBytes();
                _EnqAll(buf, cid);
            }
        }

        private void _EnqAll(byte[] buffer, int except)
        {
            foreach (var i in _dic)
                if (i.Key != except)
                    i.Value?.Enqueue(buffer);
            return;
        }

        private void _Enq(byte[] buffer, int target)
        {
            if (_dic.TryGetValue(target, out var val))
                val?.Enqueue(buffer);
            return;
        }

        private void _EnqSet(byte[] buffer, int group, int except)
        {
            if (_set.TryGetValue(group, out var res))
                foreach (var i in res)
                    if (i != except && _dic.TryGetValue(i, out var val))
                        val?.Enqueue(buffer);
            return;
        }

        private void _LinkClient_Received(object sender, LinkEventArgs<LinkPacket> arg)
        {
            var rea = arg.Record;
            var src = rea.Source;
            var tar = rea.Target;
            var buf = rea.Buffer;

            switch (tar)
            {
                case Links.ID when rea.Path == "user.group":
                    var lst = rea.Data.PullList<int>().Where(r => r < Links.ID);
                    var set = new HashSet<int>(lst);
                    if (set.Count > Links.Group)
                        throw new LinkException(LinkError.GroupLimited, "Group count out of range.");
                    lock (_loc)
                        _ResetGroup(src, set);
                    return;

                case Links.ID:
                    lock (_loc)
                        _EnqAll(buf, src);
                    return;

                case int _ when tar > Links.ID:
                    lock (_loc)
                        _Enq(buf, tar);
                    return;

                default:
                    lock (_loc)
                        _EnqSet(buf, tar, src);
                    return;
            }
        }
    }
}

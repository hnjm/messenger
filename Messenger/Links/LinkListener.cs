using Mikodev.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Mikodev.Network
{
    public sealed partial class LinkListener
    {
        internal bool _started = false;

        internal readonly int _climit = Links.ServerSocketLimit;
        internal readonly int _port = Links.ListenPort;
        internal readonly object _loc = new object();

        internal readonly Socket _soc = null;
        internal readonly Dictionary<int, LinkClient> _dic = new Dictionary<int, LinkClient>();
        internal readonly Dictionary<int, HashSet<int>> _gro = new Dictionary<int, HashSet<int>>();
        internal readonly Dictionary<int, HashSet<int>> _set = new Dictionary<int, HashSet<int>>();

        public LinkListener(int port = Links.ListenPort, int count = Links.ServerSocketLimit)
        {
            if (count < 1)
                throw new ArgumentOutOfRangeException(nameof(count), "Count limit must bigger than zero!");
            var iep = new IPEndPoint(IPAddress.Any, port);
            var soc = new Socket(SocketType.Stream, ProtocolType.Tcp);

            try
            {
                soc.Bind(iep);
                soc.Listen(count);
            }
            catch (Exception)
            {
                soc.Dispose();
                throw;
            }

            _soc = soc;
            _port = port;
            _climit = count;
        }

        public Task Listen()
        {
            lock (_loc)
            {
                if (_started)
                    throw new InvalidOperationException();
                _started = true;
            }
            return _Listen();
        }

        private async Task _Listen()
        {
            void _Invoke(Socket soc) => Task.Run(() => _Connect(soc)).ContinueWith(tsk =>
            {
                var ex = tsk.Exception;
                if (ex == null)
                    return;
                Log.Error(ex);
                soc.Dispose();
            });

            while (true)
            {
                try
                {
                    var clt = await _soc.AcceptAsyncEx();
                    _Invoke(clt);
                }
                catch (SocketException ex)
                {
                    Log.Error(ex);
                }
            }
        }

        private void _Connect(Socket socket)
        {
            LinkError _Check(int code)
            {
                if (code <= Links.Id)
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
            var err = LinkError.None;
            var cid = 0;
            var iep = default(IPEndPoint);
            var oep = default(IPEndPoint);

            byte[] _Response(byte[] buf)
            {
                var rea = new PacketReader(buf);
                var rsa = new RSACryptoServiceProvider();
                if (string.Equals(rea["protocol"].Pull<string>(), Links.Protocol, StringComparison.InvariantCultureIgnoreCase) == false)
                    throw new LinkException(LinkError.ProtocolMismatch);
                cid = rea["source"].Pull<int>();
                err = _Check(cid);
                rsa.FromXmlString(rea["rsakey"].Pull<string>());
                iep = rea["endpoint"].Pull<IPEndPoint>();
                oep = (IPEndPoint)socket.RemoteEndPoint;
                var res = PacketWriter.Serialize(new
                {
                    result = err,
                    aeskey = rsa.Encrypt(key, true),
                    aesiv = rsa.Encrypt(blk, true),
                    endpoint = oep,
                });
                return res.GetBytes();
            }

            try
            {
                var buf = socket.ReceiveAsyncExt().WaitTimeout("Listener request timeout.");
                var res = _Response(buf);
                socket.SendAsyncExt(res).WaitTimeout("Listener response timeout.");
                LinkException.ThrowError(err);
            }
            catch (Exception)
            {
                if (err == LinkError.Success)
                    lock (_loc)
                        _dic.Remove(cid);
                throw;
            }

            var clt = new LinkClient(cid, socket, iep, oep) { _key = key, _blk = blk };
            clt.Received += _ClientReceived;
            clt.Disposed += _ClientDisposed;

            lock (_loc)
            {
                _dic.Remove(cid);
                _dic.Add(cid, clt);
                _gro.Add(cid, new HashSet<int>());
            }

            clt.Start();
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

        private void _ClientDisposed(object sender, EventArgs e)
        {
            var clt = (LinkClient)sender;
            var cid = clt._id;
            var wtr = PacketWriter.Serialize(new
            {
                source = Links.Id,
                target = Links.Id,
                path = "user.list",
            });

            lock (_loc)
            {
                _ResetGroup(cid);
                var lst = _dic.Where(r => r.Value != null).Select(r => r.Key);
                var buf = wtr.PushList("data", lst).GetBytes();
                _EnqAll(buf, cid);
            }
            clt.Received -= _ClientReceived;
            clt.Disposed -= _ClientDisposed;
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

        private void _ClientReceived(object sender, LinkEventArgs<LinkPacket> arg)
        {
            var rea = arg.Object;
            var src = rea.Source;
            var tar = rea.Target;
            var buf = rea.Buffer;

            switch (tar)
            {
                case Links.Id when rea.Path == "user.group":
                    var lst = rea.Data.PullList<int>().Where(r => r < Links.Id);
                    var set = new HashSet<int>(lst);
                    if (set.Count > Links.GroupLabelLimit)
                        throw new LinkException(LinkError.GroupLimited);
                    lock (_loc)
                        _ResetGroup(src, set);
                    return;

                case Links.Id:
                    lock (_loc)
                        _EnqAll(buf, src);
                    return;

                case int _ when tar > Links.Id:
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

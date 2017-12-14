using Mikodev.Logger;
using System;
using System.Collections.Concurrent;
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
        internal readonly object _locker = new object();

        internal readonly Socket _socket = null;
        internal readonly ConcurrentDictionary<int, LinkClient> _clients = new ConcurrentDictionary<int, LinkClient>();
        internal readonly Dictionary<int, HashSet<int>> _joined = new Dictionary<int, HashSet<int>>();
        internal readonly ConcurrentDictionary<int, ConcurrentDictionary<int, LinkClient>> _set = new ConcurrentDictionary<int, ConcurrentDictionary<int, LinkClient>>();

        public LinkListener(int port = Links.ListenPort, int count = Links.ServerSocketLimit)
        {
            if (count < 1 || count > Links.ServerSocketLimit)
                throw new ArgumentOutOfRangeException(nameof(count), "Count limit out of range!");
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

            _socket = soc;
            _port = port;
            _climit = count;
        }

        public Task Listen()
        {
            lock (_locker)
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
                var soc = default(Socket);

                try
                {
                    soc = await _socket.AcceptAsyncEx();
                    soc.SetKeepAlive();
                    _Invoke(soc);
                }
                catch (SocketException ex)
                {
                    Log.Error(ex);
                    soc?.Dispose();
                }
            }
        }

        private void _Connect(Socket socket)
        {
            LinkError _Check(int code)
            {
                if (code <= Links.Id)
                    return LinkError.IdInvalid;
                if (_clients.Count >= _climit)
                    return LinkError.CountLimited;
                lock (_locker)
                {
                    return _clients.TryAdd(code, null) ? LinkError.Success : LinkError.IdConflict;
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
                err.AssertError();
            }
            catch (Exception)
            {
                if (err == LinkError.Success)
                    lock (_locker)
                        _clients.TryRemove(cid, out var _).AssertFatal("Failed to remove placeholder!");
                throw;
            }

            var clt = new LinkClient(cid, socket, iep, oep) { _key = key, _blk = blk };
            clt.Received += _ClientReceived;
            clt.Disposed += _ClientDisposed;

            lock (_locker)
            {
                _clients.TryUpdate(cid, clt, null).AssertFatal("Failed to update client!");
                _joined.Add(cid, new HashSet<int>());
            }

            clt.Start();
        }

        private void _ResetGroup(LinkClient client, IEnumerable<int> target = null)
        {
            /* Require lock */
            var cid = client._id;
            var set = _joined[cid];
            foreach (var i in set)
            {
                _set.TryGetValue(i, out var gro).AssertFatal("Failed to get group collection!");
                gro.TryRemove(cid, out var _).AssertFatal("Failed to remove client from group collection!");
                if (gro.Count > 0)
                    continue;
                _set.TryRemove(i, out var _).AssertFatal("Failed to remove empty group!");
            }

            if (target == null)
            {
                _joined.Remove(cid);
                _clients.TryRemove(cid, out var _).AssertFatal("Failed to remove client!");
                return;
            }

            // Union with -> add range
            set.Clear();
            set.UnionWith(target);
            foreach (var i in target)
            {
                var gro = _set.GetOrAdd(i, _ => new ConcurrentDictionary<int, LinkClient>());
                gro.TryAdd(cid, client).AssertFatal("Failed to add client to group collection!");
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

            lock (_locker)
            {
                _ResetGroup(clt);
                var lst = _clients.Where(r => r.Value != null).Select(r => r.Key);
                var buf = wtr.PushList("data", lst).GetBytes();
                _EnqAll(buf, cid);
            }
            clt.Received -= _ClientReceived;
            clt.Disposed -= _ClientDisposed;
        }

        private void _EnqAll(byte[] buffer, int except)
        {
            foreach (var val in _clients.Values)
                if (val != null && val._id != except)
                    val.Enqueue(buffer);
            return;
        }

        private void _ClientReceived(object sender, LinkEventArgs<LinkPacket> arg)
        {
            var clt = (LinkClient)sender;
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
                    lock (_locker)
                        _ResetGroup(clt, set);
                    return;

                case Links.Id:
                    _EnqAll(buf, src);
                    return;

                case int _ when tar > Links.Id:
                    // Thread safe operation
                    if (_clients.TryGetValue(tar, out var val))
                        val?.Enqueue(buf);
                    return;

                default:
                    // Thread safe operation
                    if (_set.TryGetValue(tar, out var res))
                        foreach (var obj in res.Values)
                            if (obj != null && obj._id != src)
                                obj.Enqueue(buf);
                    return;
            }
        }
    }
}

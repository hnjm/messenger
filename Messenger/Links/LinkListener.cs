using Mikodev.Logger;
using System;
using System.Collections.Concurrent;
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
        internal const int _NoticeDelay = 100;
        internal static readonly TimeSpan _NoticeInterval = TimeSpan.FromMilliseconds(1000);

        internal int _started = 0;

        internal readonly int _climit = Links.ServerSocketLimit;
        internal readonly int _port = Links.Port;
        internal readonly object _locker = new object();

        internal readonly Socket _socket = null;
        internal readonly LinkNoticeSource _notice = new LinkNoticeSource(_NoticeInterval);
        internal readonly ConcurrentDictionary<int, LinkClient> _clients = new ConcurrentDictionary<int, LinkClient>();
        internal readonly ConcurrentDictionary<int, HashSet<int>> _joined = new ConcurrentDictionary<int, HashSet<int>>();
        internal readonly ConcurrentDictionary<int, ConcurrentDictionary<int, LinkClient>> _set = new ConcurrentDictionary<int, ConcurrentDictionary<int, LinkClient>>();

        public LinkListener(int port = Links.Port, int count = Links.ServerSocketLimit)
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
            if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
                throw new InvalidOperationException("Listen task already started!");

            _Notice().ContinueWith(task => Log.Error(task.Exception));
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
                return _clients.TryAdd(code, null) ? LinkError.Success : LinkError.IdConflict;
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
                    _clients.TryRemove(cid, out var val).AssertFatal(val == null, "Failed to remove placeholder!");
                throw;
            }

            var clt = new LinkClient(cid, socket, iep, oep) { _key = key, _blk = blk };
            _clients.TryUpdate(cid, clt, null).AssertFatal("Failed to update client!");

            clt.Received += _ClientReceived;
            clt.Disposed += _ClientDisposed;
            clt.Start();
        }

        private void _ClientReset(LinkClient client, IEnumerable<int> groups = null)
        {
            /* Require lock */
            var cid = client._id;
            var set = _joined.GetOrAdd(cid, _ => new HashSet<int>());
            foreach (var i in set)
            {
                _set.TryGetValue(i, out var gro).AssertFatal("Failed to get group collection!");
                gro.TryRemove(cid, out var val).AssertFatal(val == client, "Failed to remove client from group collection!");
                if (gro.Count > 0)
                    continue;
                _set.TryRemove(i, out var res).AssertFatal(res == gro, "Failed to remove empty group!");
            }

            if (groups == null)
            {
                /* Client shutdown */
                _joined.TryRemove(cid, out var res).AssertFatal(res == set, "Failed to remove group set!");
                _clients.TryRemove(cid, out var val).AssertFatal(val == client, "Failed to remove client!");
                return;
            }

            // Union with -> add range
            set.Clear();
            set.UnionWith(groups);
            foreach (var i in groups)
            {
                var gro = _set.GetOrAdd(i, _ => new ConcurrentDictionary<int, LinkClient>());
                gro.TryAdd(cid, client).AssertFatal("Failed to add client to group collection!");
            }
        }

        private async Task _Notice()
        {
            while (true)
            {
                await Task.Delay(_NoticeDelay);
                var res = _notice.Notice();
                if (res.IsAny == false)
                    continue;

                var lst = _clients.Where(r => r.Value != null).Select(r => r.Key).ToList();
                var wtr = PacketWriter.Serialize(new
                {
                    source = Links.Id,
                    target = Links.Id,
                    path = "user.list",
                    data = lst,
                });

                var buf = wtr.GetBytes();
                foreach (var i in _clients.Values)
                    i?.Enqueue(buf);
                res.Handled();
            }
        }

        private void _ClientDisposed(object sender, EventArgs e)
        {
            var clt = (LinkClient)sender;

            lock (_locker)
                _ClientReset(clt);
            _notice.Update();

            clt.Received -= _ClientReceived;
            clt.Disposed -= _ClientDisposed;
        }

        private void _ClientReceived(object sender, LinkEventArgs<LinkPacket> arg)
        {
            var obj = arg.Object;
            var src = obj.Source;
            var tar = obj.Target;
            var buf = obj.Buffer;

            if (tar == Links.Id)
            {
                if (obj.Path == "user.group")
                {
                    var lst = obj.Data.PullList<int>().Where(r => r < Links.Id);
                    var set = new HashSet<int>(lst);
                    if (set.Count > Links.GroupLabelLimit)
                        throw new LinkException(LinkError.GroupLimited);
                    var clt = (LinkClient)sender;
                    lock (_locker)
                        _ClientReset(clt, set);
                    return;
                }

                foreach (var val in _clients.Values)
                    if (val != null && val._id != src)
                        val.Enqueue(buf);
                return;
            }
            else if (tar > Links.Id)
            {
                // Thread safe operation
                if (_clients.TryGetValue(tar, out var val))
                    val?.Enqueue(buf);
                return;
            }
            else
            {
                // Thread safe operation
                if (_set.TryGetValue(tar, out var grp))
                    foreach (var val in grp.Values)
                        if (val != null && val._id != src)
                            val.Enqueue(buf);
                return;
            }
        }
    }
}

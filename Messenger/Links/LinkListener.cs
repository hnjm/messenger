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
        internal const int _NoticeDelay = 100;

        internal static readonly TimeSpan _NoticeInterval = TimeSpan.FromMilliseconds(1000);

        internal readonly object _locker = new object();

        internal readonly string _sname = null;

        internal readonly int _climit = Links.ServerSocketLimit;

        internal readonly int _port = Links.Port;

        internal readonly Socket _broadcast = null;

        internal readonly Socket _socket = null;

        internal readonly LinkNoticeSource _notice = new LinkNoticeSource(_NoticeInterval);

        internal readonly ConcurrentDictionary<int, LinkClient> _clients = new ConcurrentDictionary<int, LinkClient>();

        internal readonly ConcurrentDictionary<int, HashSet<int>> _joined = new ConcurrentDictionary<int, HashSet<int>>();

        internal readonly ConcurrentDictionary<int, ConcurrentDictionary<int, LinkClient>> _set = new ConcurrentDictionary<int, ConcurrentDictionary<int, LinkClient>>();

        private LinkListener(Socket socket, Socket broadcast, int port, int count, string name)
        {
            _socket = socket;
            _broadcast = broadcast;
            _port = port;
            _climit = count;
            _sname = name;
        }

        public static async Task Run(IPAddress address, int port = Links.Port, int broadcast = Links.BroadcastPort, int count = Links.ServerSocketLimit, string name = null)
        {
            if (address == null)
                throw new ArgumentNullException(nameof(address));
            if (count < 1 || count > Links.ServerSocketLimit)
                throw new ArgumentOutOfRangeException(nameof(count), "Count limit out of range!");
            var iep = new IPEndPoint(address, port);
            var bep = new IPEndPoint(address, broadcast);
            var soc = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            var bro = new Socket(address.AddressFamily, SocketType.Dgram, ProtocolType.Udp);

            try
            {
                if (string.IsNullOrEmpty(name))
                    name = Dns.GetHostName();
                soc.Bind(iep);
                bro.Bind(bep);
                soc.Listen(count);

                var lis = new LinkListener(soc, bro, port, count, name);
                await Task.WhenAll(new Task[]
                {
                    Task.Run(lis.Notice),
                    Task.Run(lis.Listen),
                    Task.Run(lis.Broadcast),
                });
            }
            catch (Exception)
            {
                soc.Dispose();
                bro.Dispose();
                throw;
            }
        }

        private async Task Listen()
        {
            void _Invoke(Socket soc) => Task.Run(async () =>
            {
                try
                {
                    _ = soc.SetKeepAlive();
                    await await AcceptClient(soc);
                }
                finally
                {
                    soc.Dispose();
                }
            });

            while (true)
            {
                _Invoke(await _socket.AcceptAsyncEx());
            }
        }

        private async Task<Task> AcceptClient(Socket socket)
        {
            LinkError _Check(int id)
            {
                if ((Links.Id < id && id < Links.DefaultId) == false)
                    return LinkError.IdInvalid;
                if (_clients.Count >= _climit)
                    return LinkError.CountLimited;
                return _clients.TryAdd(id, null) ? LinkError.Success : LinkError.IdConflict;
            }

            var key = LinkCrypto.GetKey();
            var blk = LinkCrypto.GetBlock();
            var err = LinkError.None;
            var cid = 0;
            var iep = default(IPEndPoint);
            var oep = default(IPEndPoint);

            byte[] _Response(byte[] buf)
            {
                var rea = LinkExtension.Generator.AsToken(buf);
                if (string.Equals(rea["protocol"].As<string>(), Links.Protocol, StringComparison.InvariantCultureIgnoreCase) == false)
                    throw new LinkException(LinkError.ProtocolMismatch);
                cid = rea["source"].As<int>();
                var mod = rea["rsa"]["modulus"].As<byte[]>();
                var exp = rea["rsa"]["exponent"].As<byte[]>();
                iep = rea["endpoint"].As<IPEndPoint>();
                oep = (IPEndPoint)socket.RemoteEndPoint;
                err = _Check(cid);
                var rsa = RSA.Create();
                var par = new RSAParameters() { Exponent = exp, Modulus = mod };
                rsa.ImportParameters(par);
                var res = LinkExtension.Generator.ToBytes(new
                {
                    result = err,
                    endpoint = oep,
                    aes = new
                    {
                        key = rsa.Encrypt(key, RSAEncryptionPadding.OaepSHA1),
                        iv = rsa.Encrypt(blk, RSAEncryptionPadding.OaepSHA1),
                    }
                });
                return res;
            }

            try
            {
                var buf = await socket.ReceiveAsyncExt().TimeoutAfter("Listener request timeout.");
                var res = _Response(buf);
                await socket.SendAsyncExt(res).TimeoutAfter("Listener response timeout.");
                err.AssertError();
            }
            catch (Exception)
            {
                if (err == LinkError.Success)
                    _clients.TryRemove(cid, out var val).AssertFatal(val == null, "Failed to remove placeholder!");
                throw;
            }

            var clt = new LinkClient(cid, socket, iep, oep, key, blk);
            _clients.TryUpdate(cid, clt, null).AssertFatal("Failed to update client!");

            clt.Received += ClientReceived;
            clt.Disposed += ClientDisposed;
            return clt.Start();
        }

        private void Refresh(LinkClient client, IEnumerable<int> groups = null)
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

        private async Task Notice()
        {
            while (true)
            {
                await Task.Delay(_NoticeDelay);
                var notice = _notice.Notice();
                if (notice.IsAny == false)
                    continue;

                var list = _clients.Where(r => r.Value != null).Select(r => r.Key).ToList();
                var buffer = LinkExtension.Generator.ToBytes(new
                {
                    source = Links.Id,
                    target = Links.Id,
                    path = "user.list",
                    data = list,
                });

                foreach (var client in _clients.Values)
                    client?.Enqueue(buffer);
                notice.Handled();
            }
        }

        private void ClientDisposed(object sender, EventArgs e)
        {
            var clt = (LinkClient)sender;

            lock (_locker)
                Refresh(clt);
            _notice.Update();

            clt.Received -= ClientReceived;
            clt.Disposed -= ClientDisposed;
        }

        private void ClientReceived(object sender, LinkEventArgs<LinkPacket> arg)
        {
            var packet = arg.Object;
            var source = packet.Source;
            var target = packet.Target;
            var buffer = packet.Buffer;

            if (target == Links.Id)
            {
                if (packet.Path == "user.group")
                {
                    var list = new HashSet<int>(packet.Data.As<int[]>().Where(r => r < Links.Id));
                    if (list.Count > Links.GroupLabelLimit)
                        throw new LinkException(LinkError.GroupLimited);
                    var client = (LinkClient)sender;
                    lock (_locker)
                        Refresh(client, list);
                    return;
                }

                foreach (var value in _clients.Values)
                    if (value != null && value._id != source)
                        value.Enqueue(buffer);
                return;
            }
            else if (target > Links.Id)
            {
                // Thread safe operation
                if (_clients.TryGetValue(target, out var val))
                    val?.Enqueue(buffer);
                return;
            }
            else
            {
                // Thread safe operation
                if (_set.TryGetValue(target, out var grp))
                    foreach (var val in grp.Values)
                        if (val != null && val._id != source)
                            val.Enqueue(buffer);
                return;
            }
        }

        private async Task Broadcast()
        {
            while (true)
            {
                var available = _broadcast.Available;
                if (available < 1)
                {
                    await Task.Delay(Links.Delay);
                    continue;
                }

                try
                {
                    var buffer = new byte[Math.Min(available, Links.BufferLength)];
                    var remote = (EndPoint)new IPEndPoint(IPAddress.Any, IPEndPoint.MinPort);
                    var length = _broadcast.ReceiveFrom(buffer, ref remote);

                    var packet = LinkExtension.Generator.AsToken(new ReadOnlyMemory<byte>(buffer, 0, length));
                    if (string.Equals(Links.Protocol, packet["protocol"].As<string>()) == false)
                        continue;
                    var res = LinkExtension.Generator.ToBytes(new
                    {
                        protocol = Links.Protocol,
                        port = _port,
                        name = _sname,
                        limit = _climit,
                        count = _clients.Count,
                    });
                    _ = _broadcast.SendTo(res, remote);
                }
                catch (SocketException ex)
                {
                    Log.Error(ex);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }
        }
    }
}

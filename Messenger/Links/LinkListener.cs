using System;
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
    public class LinkListener
    {
        internal int _started = 0;

        internal int _climit = Links.Count;

        internal readonly object _loc = new object();

        internal Socket _socket = null;

        internal Dictionary<int, LinkClient> _dic = new Dictionary<int, LinkClient>();

        internal Dictionary<int, List<int>> _groups = new Dictionary<int, List<int>>();

        public void Start(int port = Links.Port, int count = Links.Count)
        {
            if (count < 1)
                throw new ArgumentOutOfRangeException(nameof(count), $"The count limit should bigger than zero!");
            if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
                throw new InvalidOperationException("Instance already started!");

            var iep = new IPEndPoint(IPAddress.Any, port);
            var soc = new Socket(SocketType.Stream, ProtocolType.Tcp);

            try
            {
                soc.Bind(iep);
                soc.Listen(count);
            }
            catch (SocketException)
            {
                soc?.Dispose();
                throw;
            }

            _climit = count;
            _socket = soc;
            Task.Run(new Action(_Listen));
        }

        private void _Listen()
        {
            while (_socket != null)
            {
                var soc = _socket.Accept();
                soc._SetKeepAlive();
                Task.Run(() => _Handle(soc)).ContinueWith(t =>
                {
                    if (t.Exception != null)
                    {
                        Trace.WriteLine(t.Exception);
                        soc.Dispose();
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
                lock (_loc)
                {
                    if (_dic.ContainsKey(code))
                        return LinkError.CodeConflict;
                    _dic.Add(code, null);
                    return LinkError.Success;
                }
            }

            void remove(int code)
            {
                lock (_loc)
                {
                    if (_dic.TryGetValue(code, out var val) && val == null)
                        _dic.Remove(code);
                    else
                        throw new LinkException(LinkError.AssertFailed);
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
                if (Task.Run(() => client._SendExtendAsync(respond())).Wait(Links.Timeout) == false)
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

            lock (_dic)
            {
                remove(cid);
                _dic.Add(cid, clt);
                _groups.Add(cid, new List<int>());
            }

            clt.Start(client);
        }

        private void _LinkClient_Shutdown(object sender, EventArgs e)
        {
            var clt = (LinkClient)sender;
            var lst = new List<int>();
            lock (_loc)
            {
                _dic.Remove(clt._id);
                _groups.Remove(clt._id);
                foreach (var c in _dic)
                {
                    lst.Add(c.Key);
                }

                var buf = PacketWriter.Serialize(new
                {
                    source = Links.ID,
                    target = Links.ID,
                    path = "user.ids",
                    data = lst,
                }).GetBytes();

                foreach (var i in _dic)
                {
                    i.Value.Enqueue(buf);
                }
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
                    lock (_loc)
                    {
                        _groups[src].Clear();
                        _groups[src] = lst;
                    }
                    return;
                }
                lock (_loc)
                {
                    foreach (var i in _dic)
                    {
                        i.Value.Enqueue(rea.Buffer);
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
                lock (_loc)
                {
                    foreach (var i in _groups)
                    {
                        if (i.Key == src)
                            continue;
                        if (i.Value.Contains(tar))
                            _dic[i.Key].Enqueue(rea.Buffer);
                    }
                }
            }
        }
    }
}

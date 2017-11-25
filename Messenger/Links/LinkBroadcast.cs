using Mikodev.Logger;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Mikodev.Network
{
    public sealed partial class LinkListener
    {
        internal Socket _broad = null;

        internal string _sname = null;

        public Task Broadcast(int port = Links.BroadcastPort, string name = null)
        {
            var soc = new Socket(SocketType.Dgram, ProtocolType.Udp);

            try
            {
                if (string.IsNullOrEmpty(name))
                    name = Dns.GetHostName();
                soc.Bind(new IPEndPoint(IPAddress.Any, port));
                if (Interlocked.CompareExchange(ref _broad, soc, null) != null)
                    throw new InvalidOperationException("Broadcast socket not null!");
                _sname = name;
            }
            catch (Exception)
            {
                soc.Dispose();
                throw;
            }

            return _Broadcast();
        }

        internal async Task _Broadcast()
        {
            var wtr = PacketWriter.Serialize(new
            {
                protocol = Links.Protocol,
                port = _port,
                name = _sname,
                limit = _climit,
            });

            while (_broad != null)
            {
                var ava = _broad.Available;
                if (ava < 1)
                {
                    await Task.Delay(Links.Delay);
                    continue;
                }

                try
                {
                    var buf = new byte[Math.Min(ava, Links.BufferLength)];
                    var iep = (EndPoint)new IPEndPoint(IPAddress.Any, IPEndPoint.MinPort);
                    var len = _broad.ReceiveFrom(buf, ref iep);

                    var rea = new PacketReader(buf, 0, len);
                    if (string.Equals(Links.Protocol, rea["protocol", true]?.Pull<string>()) == false)
                        continue;
                    var res = wtr.Push("count", _dic.Count).GetBytes();
                    var sub = _broad.SendTo(res, iep);
                }
                catch (SocketException ex)
                {
                    Log.Error(ex);
                }
            }
        }
    }
}

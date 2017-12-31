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
        internal Socket _broadcast = null;

        internal string _sname = null;

        public Task Broadcast(int port = Links.BroadcastPort, string name = null)
        {
            var soc = new Socket(SocketType.Dgram, ProtocolType.Udp);

            try
            {
                if (string.IsNullOrEmpty(name))
                    name = Dns.GetHostName();
                soc.Bind(new IPEndPoint(IPAddress.Any, port));
                if (Interlocked.CompareExchange(ref _broadcast, soc, null) != null)
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

            while (_broadcast != null)
            {
                var ava = _broadcast.Available;
                if (ava < 1)
                {
                    await Task.Delay(Links.Delay);
                    continue;
                }

                try
                {
                    var buf = new byte[Math.Min(ava, Links.BufferLength)];
                    var iep = (EndPoint)new IPEndPoint(IPAddress.Any, IPEndPoint.MinPort);
                    var len = _broadcast.ReceiveFrom(buf, ref iep);

                    var rea = new PacketReader(buf, 0, len);
                    if (string.Equals(Links.Protocol, rea["protocol", true]?.GetValue<string>()) == false)
                        continue;
                    var res = wtr.SetValue("count", _clients.Count).GetBytes();
                    var sub = _broadcast.SendTo(res, iep);
                }
                catch (SocketException ex)
                {
                    Log.Error(ex);
                }
            }
        }
    }
}

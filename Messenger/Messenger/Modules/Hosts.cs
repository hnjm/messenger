using Messenger.Extensions;
using Messenger.Models;
using Mikodev.Network;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Messenger.Modules
{
    internal class Hosts
    {
        public const int DefaultTimeout = 1000;

        public const string KeyLast = "server-last";
        public const string KeyList = "server-list";
        public const string KeyPort = "server-port";

        private IPEndPoint _broadcast = null;
        private string _host = null;
        private int _port = 0;
        private IEnumerable<IPEndPoint> _points = new List<IPEndPoint>();

        private static Hosts s_ins = new Hosts();

        public static string Name { get => s_ins._host; set => s_ins._host = value; }
        public static int Port { get => s_ins._port; set => s_ins._port = value; }

        /// <summary>
        /// 通过 UDP 广播从搜索列表搜索服务器
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<Host> Refresh()
        {
            var lst = new List<Host>();
            var buf = new byte[Links.Buffer];
            var soc = new Socket(SocketType.Dgram, ProtocolType.Udp);
            var wth = new Stopwatch();
            var txt = new PacketWriter().Push("protocol", Links.Protocol).GetBytes();
            void action()
            {
                while (soc != null)
                {
                    var iep = new IPEndPoint(IPAddress.Any, IPEndPoint.MinPort) as EndPoint;
                    var len = soc.ReceiveFrom(buf, ref iep);
                    var tmp = new byte[len];
                    Array.Copy(buf, 0, tmp, 0, len);
                    var rea = new PacketReader(tmp);
                    var inf = new Host()
                    {
                        Protocol = rea["protocol"].Pull<string>(),
                        Port = rea["port"].Pull<int>(),
                        Name = rea["name"].Pull<string>(),
                        Count = rea["count"].Pull<int>(),
                        CountLimit = rea["limit"].Pull<int>(),
                    };

                    if (!inf.Protocol.Equals(Links.Protocol))
                        continue;
                    inf.Address = (iep as IPEndPoint).Address;
                    inf.Delay = wth.ElapsedMilliseconds;
                    if (lst.Find((r) => r.Equals(inf)) == null)
                        lst.Add(inf);
                }
            }
            try
            {
                soc.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
                soc.Bind(new IPEndPoint(IPAddress.Any, 0));
                wth.Start();
                foreach (var a in s_ins._points)
                    soc.SendTo(txt, a);
                if (Task.Run(new Action(action)).Wait(DefaultTimeout) == false)
                    throw new TimeoutException();
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
            }

            soc?.Dispose();
            soc = null;
            return lst;
        }

        /// <summary>
        /// 读取服务器搜索列表
        /// </summary>
        [AutoLoad(4, AutoLoadFlag.OnLoad)]
        public static void Load()
        {
            var lst = new List<IPEndPoint>();
            try
            {
                var pot = Options.GetOption(KeyPort, Links.BroadcastPort.ToString());
                if (pot != null)
                    s_ins._broadcast = new IPEndPoint(IPAddress.Broadcast, int.Parse(pot));
                var str = Options.GetOption(KeyLast);
                Converts.GetHost(str, out s_ins._host, out s_ins._port);
                var sts = Options.GetOption(KeyList) ?? string.Empty;
                var arr = sts.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var s in arr)
                    lst.Add(s._ToEndPoint());
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
            }
            if (s_ins._broadcast != null)
                lst.Add(s_ins._broadcast);
            s_ins._points = lst.Distinct();
        }

        /// <summary>
        /// 保存列表到文件
        /// </summary>
        [AutoLoad(32, AutoLoadFlag.OnExit)]
        public static void Save()
        {
            var stb = new StringBuilder();
            var eps = s_ins._points?.ToList();
            if (eps != null)
            {
                var idx = 0;
                while (idx < eps.Count)
                {
                    stb.Append(eps[idx]);
                    if (idx < eps.Count - 1)
                        stb.Append('|');
                    idx++;
                }
            }
            if (s_ins._host != null)
                Options.SetOption(KeyLast, $"{s_ins._host}:{s_ins._port}");
            Options.SetOption(KeyList, stb.ToString());
        }
    }
}

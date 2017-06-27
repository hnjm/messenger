using Messenger.Foundation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Messenger
{
    class ModuleServer
    {
        public const int DefaultTimeout = 1000;
        public const int DefaultBufferSize = 32 * 1024;

        public const string KeyLast = "server-last";
        public const string KeyList = "server-list";
        public const string KeyPort = "server-port";

        private IPEndPoint broadcast = null;
        private IPEndPoint current = null;
        private IEnumerable<IPEndPoint> points = new List<IPEndPoint>();

        private static ModuleServer instance = new ModuleServer();

        public static IPEndPoint Current { get => instance.current; set => instance.current = value; }

        /// <summary>
        /// 通过 UDP 广播从搜索列表搜索服务器
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<ItemServer> Refresh()
        {
            var lst = new List<ItemServer>();
            var buf = new byte[DefaultBufferSize];
            var soc = default(Socket);
            var wth = new Stopwatch();
            var txt = Xml.Serialize(Server.Protocol);
            var act = new Action(() =>
                {
                    while (soc != null)
                    {
                        var iep = new IPEndPoint(IPAddress.Any, IPEndPoint.MinPort) as EndPoint;
                        var len = soc.ReceiveFrom(buf, ref iep);
                        var inf = Xml.Deserialize<ItemServer>(buf, 0, len);

                        if (!inf.Protocol.Equals(Server.Protocol))
                            continue;
                        inf.Address = (iep as IPEndPoint).Address;
                        inf.Delay = wth.ElapsedMilliseconds;
                        if (lst.Find((r) => r.Equals(inf)) == null)
                            lst.Add(inf);
                    }
                });
            try
            {
                soc = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                soc.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
                soc.Bind(new IPEndPoint(IPAddress.Any, 0));
                wth.Start();
                foreach (var a in instance.points)
                    soc.SendTo(txt, a);
                Extension.TimeoutInvoke(act, DefaultTimeout);
            }
            catch (Exception ex)
            {
                if (ex.InnerException != null)
                    Log.E(nameof(ModuleServer), ex.InnerException, "服务器搜索内部异常信息:");
                Log.E(nameof(ModuleServer), ex, "服务器搜索结束.");
            }

            soc?.Dispose();
            soc = null;
            return lst;
        }

        /// <summary>
        /// 读取服务器搜索列表
        /// </summary>
        /// <returns></returns>
        public static void Load()
        {
            var lst = new List<IPEndPoint>();
            try
            {
                var pot = ModuleOption.GetOption(KeyPort, Broadcast.DefaultPort.ToString());
                if (pot != null)
                    instance.broadcast = new IPEndPoint(IPAddress.Broadcast, int.Parse(pot));
                var str = ModuleOption.GetOption(KeyLast);
                if (str != null)
                    instance.current = str.ToEndPoint();
                var sts = ModuleOption.GetOption(KeyList);
                var arr = sts.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var s in arr)
                    lst.Add(s.ToEndPoint());
            }
            catch (Exception ex)
            {
                Log.E(nameof(ModuleServer), ex, "读取配置出错");
            }
            if (instance.broadcast != null)
                lst.Add(instance.broadcast);
            instance.points = lst.Distinct();
        }

        /// <summary>
        /// 保存列表到文件
        /// </summary>
        public static void Save()
        {
            var stb = new StringBuilder();
            var eps = instance.points?.ToList();
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
            ModuleOption.SetOption(KeyList, stb.ToString());
            ModuleOption.SetOption(KeyLast, instance.current?.ToString());
        }
    }
}

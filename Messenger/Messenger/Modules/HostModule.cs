﻿using Messenger.Extensions;
using Messenger.Models;
using Mikodev.Logger;
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
    /// <summary>
    /// 搜索和管理服务器信息
    /// </summary>
    internal class HostModule
    {
        private const int _Timeout = 1000;
        private const string _KeyLast = "server-last";
        private const string _KeyList = "server-list";
        private const string _KeyPort = "server-port";

        private IPEndPoint _broadcast = null;
        private string _host = null;
        private int _port = 0;
        private IEnumerable<IPEndPoint> _points = new List<IPEndPoint>();

        private static HostModule s_ins = new HostModule();

        public static string Name { get => s_ins._host; set => s_ins._host = value; }
        public static int Port { get => s_ins._port; set => s_ins._port = value; }

        /// <summary>
        /// 通过 UDP 广播从搜索列表搜索服务器
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<Host> Refresh()
        {
            var lst = new List<Host>();
            var stw = new Stopwatch();
            var soc = new Socket(SocketType.Dgram, ProtocolType.Udp);
            var txt = new PacketWriter().Push("protocol", Links.Protocol).GetBytes();

            Host gethost(byte[] buffer, int offset, int length)
            {
                try
                {
                    var rea = new PacketReader(buffer, offset, length);
                    var inf = new Host()
                    {
                        Protocol = rea["protocol"].Pull<string>(),
                        Port = rea["port"].Pull<int>(),
                        Name = rea["name"].Pull<string>(),
                        Count = rea["count"].Pull<int>(),
                        CountLimit = rea["limit"].Pull<int>(),
                    };
                    return inf;
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                    return null;
                }
            }

            async Task _Refresh()
            {
                while (soc != null)
                {
                    var ava = soc.Available;
                    if (ava < 1)
                    {
                        await Task.Delay(Links.Delay);
                        continue;
                    }

                    var buf = new byte[Math.Min(ava, Links.BufferLength)];
                    var iep = new IPEndPoint(IPAddress.Any, IPEndPoint.MinPort) as EndPoint;
                    var len = soc.ReceiveFrom(buf, ref iep);
                    var inf = gethost(buf, 0, len);

                    if (inf == null || inf.Protocol.Equals(Links.Protocol) == false)
                        continue;
                    inf.Address = ((IPEndPoint)iep).Address;
                    inf.Delay = stw.ElapsedMilliseconds;

                    if (lst.Find((r) => r.Equals(inf)) == null) lst.Add(inf);
                }
            }

            try
            {
                soc.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
                soc.Bind(new IPEndPoint(IPAddress.Any, 0));
                stw.Start();

                foreach (var a in s_ins._points)
                    soc.SendTo(txt, a);
                _Refresh().Wait(_Timeout);
            }
            catch (Exception ex) when (ex is SocketException || ex is AggregateException)
            {
                Log.Error(ex);
            }

            soc?.Dispose();
            soc = null;
            stw.Stop();
            return lst;
        }

        /// <summary>
        /// 读取服务器搜索列表
        /// </summary>
        [AutoLoad(4, AutoLoadFlags.OnLoad)]
        public static void Load()
        {
            var lst = new List<IPEndPoint>();
            try
            {
                var pot = OptionModule.GetOption(_KeyPort, Links.BroadcastPort.ToString());
                if (pot != null)
                    s_ins._broadcast = new IPEndPoint(IPAddress.Broadcast, int.Parse(pot));
                var str = OptionModule.GetOption(_KeyLast);
                Converts.TryGetHostEx(str, out s_ins._host, out s_ins._port);
                var sts = OptionModule.GetOption(_KeyList) ?? string.Empty;
                var arr = sts.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var s in arr)
                    lst.Add(s.ToEndPointEx());
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
            if (s_ins._broadcast != null)
                lst.Add(s_ins._broadcast);
            s_ins._points = lst.Distinct();
        }

        /// <summary>
        /// 保存列表到文件
        /// </summary>
        [AutoLoad(32, AutoLoadFlags.OnExit)]
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
                OptionModule.SetOption(_KeyLast, $"{s_ins._host}:{s_ins._port}");
            OptionModule.SetOption(_KeyList, stb.ToString());
        }
    }
}
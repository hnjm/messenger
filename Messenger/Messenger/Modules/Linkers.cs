using Messenger.Extensions;
using Messenger.Models;
using Mikodev.Network;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace Messenger.Modules
{
    /// <summary>
    /// 维持客户端与服务器的连接, 并负责引发事件
    /// </summary>
    internal class Linkers
    {
        private readonly object _loc = new object();

        private LinkClient _clt = null;

        private EventHandler<LinkEventArgs<Guid>> _Request = null;

        private Socket _soc = null;

        private static Linkers s_ins = new Linkers();

        public static int ID => s_ins._clt?.ID ?? Profiles.Current.ID;

        public static bool IsRunning => s_ins._clt?.IsRunning ?? false;

        public static event EventHandler<LinkEventArgs<Guid>> Requests { add => s_ins._Request += value; remove => s_ins._Request -= value; }

        public static void Start(int id, IPEndPoint endpoint)
        {
            var clt = new LinkClient(id);
            clt.Received += (s, e) => Routers.Handle(e.Record);
            clt.Shutdown += (s, e) => Entrance.ShowError("连接已断开", s_ins._clt?.Exception);
            var soc = new Socket(SocketType.Stream, ProtocolType.Tcp);
            soc.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            try
            {
                clt.Start(endpoint);
                soc.Bind(clt.InnerEndPoint);
                soc.Listen(Links.Count);
                lock (s_ins._loc)
                {
                    lock (s_ins._loc)
                        if (s_ins._clt != null || s_ins._soc != null)
                            throw new InvalidOperationException();
                    s_ins._clt = clt;
                    s_ins._soc = soc;
                }
            }
            catch (Exception)
            {
                clt.Dispose();
                soc.Dispose();
                throw;
            }

            _Listen(soc).ContinueWith(tsk =>
            {
                if (tsk.Exception == null)
                    return;
                Trace.WriteLine(tsk.Exception);
            });

            Packets.OnHandled += _Packets_OnHandled;
            Ports.Expect.ListChanged += _Ports_ListChanged;
            Profiles.Current.ID = id;

            Posters.UserProfile(Links.ID);
            Posters.UserRequest();
            Posters.UserGroups();
        }

        private static async Task _Listen(Socket socket)
        {
            void _Invoke(Socket clt)
            {
                Task.Run(() =>
                {
                    var buf = default(byte[]);
                    if (Task.Run(async () => buf = await clt._ReceiveExtendAsync()).Wait(Links.Timeout) == false)
                        throw new TimeoutException("Timeout when accept transport header.");
                    var rea = new PacketReader(buf);
                    var key = rea.Pull<Guid>();
                    var arg = new LinkEventArgs<Guid>() { Record = key, Source = clt };
                    s_ins._Request?.Invoke(s_ins, arg);
                    if (arg.Finish == true)
                        return;
                    clt.Dispose();
                })
                .ContinueWith(tsk =>
                {
                    if (tsk.Exception == null)
                        return;
                    Trace.WriteLine(tsk.Exception);
                    clt.Dispose();
                });
            }

            while (true)
            {
                try
                {
                    var clt = await socket._AcceptAsync();
                    _Invoke(clt);
                }
                catch (SocketException ex)
                {
                    Trace.WriteLine(ex);
                    continue;
                }
            }
        }

        [AutoLoad(0, AutoLoadFlag.OnExit)]
        public static void Shutdown()
        {
            lock (s_ins._loc)
            {
                s_ins._clt?.Dispose();
                s_ins._clt = null;
                s_ins._soc?.Dispose();
                s_ins._soc = null;
            }

            Ports.Close();
            Profiles.Clear();
            Packets.OnHandled -= _Packets_OnHandled;
            Ports.Expect.ListChanged -= _Ports_ListChanged;
        }

        public static void Enqueue(byte[] buffer) => s_ins._clt?.Enqueue(buffer);

        /// <summary>
        /// 获取与连接关联的 NAT 内部端点和外部端点 (若二者相同 则只返回一个 且不会返回 null)
        /// </summary>
        public static List<IPEndPoint> GetEndPoints()
        {
            var lst = new List<IPEndPoint>();
            var clt = s_ins._clt;
            if (clt?.InnerEndPoint is IPEndPoint iep)
                lst.Add(iep);
            if (clt?.OuterEndPoint is IPEndPoint rep)
                lst.Add(rep);
            var res = lst._Distinct((a, b) => a.Equals(b));
            return res;
        }

        private static void _Packets_OnHandled(object sender, LinkEventArgs<Packet> e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var pro = Profiles.Query(e.Record.Groups);
                if (pro == null)
                    return;
                var hdl = new WindowInteropHelper(Application.Current.MainWindow).Handle;
                if (e.Finish == false || Application.Current.MainWindow.IsActive == false)
                    NativeMethods.FlashWindow(hdl, true);
                if (e.Finish == false || e.Cancel == true)
                    pro.Hint += 1;
            });
        }

        private static void _Ports_ListChanged(object sender, ListChangedEventArgs e)
        {
            if (sender == Ports.Expect && e.ListChangedType == ListChangedType.ItemAdded)
            {
                if (Application.Current.MainWindow.IsActive == true)
                    return;
                var hdl = new WindowInteropHelper(Application.Current.MainWindow).Handle;
                NativeMethods.FlashWindow(hdl, true);
            }
        }
    }
}

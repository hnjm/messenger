using Messenger.Extensions;
using Messenger.Models;
using Mikodev.Network;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace Messenger.Modules
{
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

            Task.Run(() => _Listen(soc)).ContinueWith(t =>
            {
                if (t.Exception == null)
                    return;
                Trace.WriteLine(t.Exception);
            });

            Packets.OnHandled += _Packets_OnHandled;
            Ports.Expect.ListChanged += _Transports_ListChanged;
            Profiles.Current.ID = id;

            Posters.UserProfile(Links.ID);
            Posters.UserRequest();
            Posters.UserGroups();
        }

        private static void _Listen(Socket socket)
        {
            while (true)
            {
                var clt = default(Socket);
                var arg = default(LinkEventArgs<Guid>);
                var buf = default(byte[]);

                try
                {
                    clt = socket.Accept();
                    if (Task.Run(async () => buf = await clt._ReceiveExtendAsync()).Wait(Links.Timeout) == false)
                        throw new TimeoutException("Timeout when accept transport header.");
                    var rea = new PacketReader(buf);
                    var key = rea.Pull<Guid>();
                    arg = new LinkEventArgs<Guid>() { Record = key, Source = clt };
                }
                catch (Exception ex) when (ex is SocketException || ex is PacketException)
                {
                    clt?.Dispose();
                    Trace.WriteLine(ex);
                    continue;
                }

                s_ins._Request?.Invoke(clt, arg);
                if (arg.Source != null)
                {
                    clt.Dispose();
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

            Packets.OnHandled -= _Packets_OnHandled;
            Ports.Expect.ListChanged -= _Transports_ListChanged;
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
            var res = lst._Distinct((a, b) => a.Equals(b)).ToList();
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

        private static void _Transports_ListChanged(object sender, ListChangedEventArgs e)
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

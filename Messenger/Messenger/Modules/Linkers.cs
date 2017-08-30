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
using System.Threading;
using System.Windows;
using System.Windows.Interop;

namespace Messenger.Modules
{
    class Linkers
    {
        private LinkClient _clt = null;

        private static Linkers s_ins = new Linkers();

        public static int ID => s_ins._clt?.ID ?? Profiles.Current.ID;

        public static bool IsRunning => s_ins._clt?.IsRunning ?? false;

        public static event EventHandler<LinkEventArgs<(Guid, Socket)>> Requests
        {
            add
            {
                var clt = s_ins._clt;
                if (clt == null)
                    return;
                // clt.Requests += value;
            }
            remove
            {
                var clt = s_ins._clt;
                if (clt == null)
                    return;
                // clt.Requests -= value;
            }
        }

        public static void Start(int id, IPEndPoint endpoint)
        {
            var clt = new LinkClient(id);
            clt.Received += (s, e) => Routers.Handle(e.Record);
            clt.Shutdown += (s, e) => Entrance.ShowError("连接已断开", s_ins?._clt?.Exception);
            clt.Start(endpoint);

            if (Interlocked.CompareExchange(ref s_ins._clt, clt, null) != null)
            {
                clt.Dispose();
                throw new InvalidOperationException();
            }

            Packets.OnHandled += ModulePacket_OnHandled;
            Transports.Expect.ListChanged += ModuleTrans_ListChanged;
            Profiles.Current.ID = id;

            Posters.UserProfile(Links.ID);
            Posters.UserRequest();
            Posters.UserGroups();
        }

        [AutoSave(0)]
        public static void Close()
        {
            var clt = Interlocked.Exchange(ref s_ins._clt, null);
            if (clt == null)
                return;
            clt.Dispose();

            Packets.OnHandled -= ModulePacket_OnHandled;
            Transports.Expect.ListChanged -= ModuleTrans_ListChanged;
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

        private static void ModulePacket_OnHandled(object sender, LinkEventArgs<Packet> e)
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

        private static void ModuleTrans_ListChanged(object sender, ListChangedEventArgs e)
        {
            if (sender == Transports.Expect && e.ListChangedType == ListChangedType.ItemAdded)
            {
                if (Application.Current.MainWindow.IsActive == true)
                    return;
                var hdl = new WindowInteropHelper(Application.Current.MainWindow).Handle;
                NativeMethods.FlashWindow(hdl, true);
            }
        }
    }
}

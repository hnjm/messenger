using Messenger.Extensions;
using Messenger.Foundation;
using Messenger.Foundation.Extensions;
using Messenger.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Interop;

namespace Messenger.Modules
{
    class Interact
    {
        private object _loc = new object();
        private Client _clt = null;

        private static Interact s_ins = new Interact();

        public static int ID => s_ins._clt?.ID ?? Profiles.Current.ID;

        public static bool IsRunning
        {
            get
            {
                var clt = s_ins._clt;
                if (clt == null)
                    return false;
                if (clt.IsStarted == true && clt.IsDisposed == false)
                    return true;
                return false;
            }
        }

        public static event EventHandler<LinkEventArgs<(Guid, Socket)>> Requests
        {
            add
            {
                var clt = s_ins._clt;
                if (clt == null)
                    return;
                clt.Requests += value;
            }
            remove
            {
                var clt = s_ins._clt;
                if (clt == null)
                    return;
                clt.Requests -= value;
            }
        }

        public static void Start(int id, IPEndPoint endpoint)
        {
            var clt = default(Client);
            try
            {
                clt = new Client(id);
                clt.Start(endpoint);
            }
            catch (SocketException ex)
            {
                Trace.WriteLine(ex);
                clt?.Dispose();
                throw;
            }

            clt.Received += (s, e) => Routers.Handle(e.Record);
            clt.Shutdown += (s, e) => Entrance.ShowError("连接已断开", s_ins?._clt?.Exception);

            lock (s_ins._loc)
            {
                if (s_ins._clt != null)
                {
                    clt.Dispose();
                    throw new InvalidOperationException();
                }
                s_ins._clt = clt;
            }

            Packets.OnHandled += ModulePacket_OnHandled;
            Transports.Expect.ListChanged += ModuleTrans_ListChanged;
            Profiles.Current.ID = id;

            Posters.UserProfile(Server.ID);
            Posters.UserRequest();
            Posters.UserGroups();
        }

        [AutoSave(0)]
        public static void Close()
        {
            var clt = default(Client);
            lock (s_ins._loc)
            {
                clt = s_ins._clt;
                s_ins._clt = null;
            }

            if (clt == null)
                return;
            clt.Dispose();

            Packets.OnHandled -= ModulePacket_OnHandled;
            Transports.Expect.ListChanged -= ModuleTrans_ListChanged;
        }

        public static void Enqueue(byte[] buffer)
        {
            var clt = s_ins._clt;
            if (clt == null)
                return;
            clt.Enqueue(buffer);
        }

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
            var res = lst.Distinct((a, b) => a.Equals(b)).ToList();
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

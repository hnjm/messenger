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
        private object _locker = new object();
        private Client _client = null;

        private static Interact _instance = new Interact();

        public static int ID => _instance._client?.ID ?? Profiles.Current.ID;

        public static bool IsRunning
        {
            get
            {
                var clt = _instance._client;
                if (clt == null)
                    return false;
                if (clt.IsStarted == true && clt.IsDisposed == false)
                    return true;
                return false;
            }
        }

        public static event EventHandler<CommonEventArgs<(Guid, Socket)>> Requests
        {
            add
            {
                var clt = _instance._client;
                if (clt == null)
                    return;
                clt.Requests += value;
            }
            remove
            {
                var clt = _instance._client;
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

            clt.Received += Client_Received;
            clt.Shutdown += Client_Shutdown;

            lock (_instance._locker)
            {
                if (_instance._client != null)
                {
                    clt.Dispose();
                    throw new InvalidOperationException();
                }
                _instance._client = clt;
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
            lock (_instance._locker)
            {
                clt = _instance._client;
                _instance._client = null;
            }

            if (clt == null)
                return;
            clt.Received -= Client_Received;
            clt.Shutdown -= Client_Shutdown;
            clt.Dispose();

            Packets.OnHandled -= ModulePacket_OnHandled;
            Transports.Expect.ListChanged -= ModuleTrans_ListChanged;
        }

        public static void Enqueue(byte[] buffer)
        {
            var clt = _instance._client;
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
            var clt = _instance._client;
            if (clt?.InnerEndPoint is IPEndPoint iep)
                lst.Add(iep);
            if (clt?.OuterEndPoint is IPEndPoint rep)
                lst.Add(rep);
            var res = lst.Distinct((a, b) => a.Equals(b)).ToList();
            return res;
        }

        private static void ModulePacket_OnHandled(object sender, CommonEventArgs<Packet> e)
        {
            Application.Current.Dispatcher.Invoke(() =>
                {
                    var pro = Profiles.Query(e.Object.Groups);
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

        private static void Client_Shutdown(object sender, EventArgs e) => Entrance.ShowError("服务器连接已断开", _instance?._client?.Exception);

        private static void Client_Received(object sender, CommonEventArgs<byte[]> e) => Routers.Handle(e.Object);
    }
}

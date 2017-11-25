using Messenger.Extensions;
using Messenger.Models;
using Mikodev.Logger;
using Mikodev.Network;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    internal class LinkModule
    {
        private readonly object _loc = new object();

        private LinkClient _clt = null;

        /// <summary>
        /// 监听反向连接 (用于文件传输)
        /// </summary>
        private Socket _soc = null;

        private static readonly LinkModule s_ins = new LinkModule();

        public static int ID => s_ins._clt?.ID ?? ProfileModule.Current.ID;

        public static bool IsRunning => s_ins._clt?.IsRunning ?? false;

        public static void Start(int id, IPEndPoint endpoint)
        {
            var clt = new LinkClient(id);
            clt.Received += (s, e) => RouteModule.Handle(e.Record);
            clt.Shutdown += (s, e) => Entrance.ShowError("连接已断开", s_ins._clt?.Exception);
            var soc = new Socket(SocketType.Stream, ProtocolType.Tcp);
            soc.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            try
            {
                clt.Start(endpoint);
                soc.Bind(clt.InnerEndPoint);
                soc.Listen(Links.ClientCountLimit);
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

            _Listen(soc).ContinueWith(tsk => Log.Error(tsk.Exception));

            HistoryModule.OnHandled += _OnHistoryHandled;
            ShareModule.PendingList.ListChanged += _PendingListChanged;
            ProfileModule.Current.ID = id;

            PostModule.UserProfile(Links.ID);
            PostModule.UserRequest();
            PostModule.UserGroups();
        }

        private static async Task _Listen(Socket socket)
        {
            while (true)
            {
                try
                {
                    var clt = await socket.AcceptAsyncEx();
#pragma warning disable 4014
                    Task.Run(() =>
                    {
                        var buf = clt.ReceiveAsyncExt().WaitTimeout("Timeout when accept transport header.");
                        var rea = new PacketReader(buf);
                        var key = rea["data"].Pull<Guid>();
                        var src = rea["source"].Pull<int>();

                        Share.Notify(src, key, clt)?.Wait();
                    })
                    .ContinueWith(tsk =>
                    {
                        Log.Error(tsk.Exception);
                        clt.Dispose();
                    });
#pragma warning restore 4014
                }
                catch (SocketException ex)
                {
                    Log.Error(ex);
                    continue;
                }
            }
        }

        [AutoLoad(0, AutoLoadFlags.OnExit)]
        public static void Shutdown()
        {
            lock (s_ins._loc)
            {
                s_ins._clt?.Dispose();
                s_ins._clt = null;
                s_ins._soc?.Dispose();
                s_ins._soc = null;
            }

            ShareModule.Close();
            ProfileModule.Clear();
            HistoryModule.OnHandled -= _OnHistoryHandled;
            ShareModule.PendingList.ListChanged -= _PendingListChanged;
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
            var res = lst.DistinctEx((a, b) => a.Equals(b));
            return res;
        }

        private static void _OnHistoryHandled(object sender, LinkEventArgs<Packet> e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var pro = ProfileModule.Query(e.Record.Groups);
                if (pro == null)
                    return;
                var hdl = new WindowInteropHelper(Application.Current.MainWindow).Handle;
                if (e.Finish == false || Application.Current.MainWindow.IsActive == false)
                    NativeMethods.FlashWindow(hdl, true);
                if (e.Finish == false || e.Cancel == true)
                    pro.Hint += 1;
            });
        }

        private static void _PendingListChanged(object sender, ListChangedEventArgs e)
        {
            if (sender == ShareModule.PendingList && e.ListChangedType == ListChangedType.ItemAdded)
            {
                if (Application.Current.MainWindow.IsActive == true)
                    return;
                var hdl = new WindowInteropHelper(Application.Current.MainWindow).Handle;
                NativeMethods.FlashWindow(hdl, true);
            }
        }
    }
}

using Messenger.Extensions;
using Messenger.Models;
using Mikodev.Network;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
        private LinkModule() { }
        private static readonly LinkModule s_ins = new LinkModule();
        private LinkClient _client = null;
        private readonly object _locker = new object();

        public static int Id => s_ins._client?.Id ?? ProfileModule.Id;

        public static bool IsRunning => s_ins._client?.IsRunning ?? false;

        /// <summary>
        /// 启动连接 (与 <see cref="Shutdown"/> 方法为非完全线程安全的关系, 不过两个方法不可能同时调用)
        /// </summary>
        public static void Start(int id, IPEndPoint endpoint)
        {
            var clt = new LinkClient(id, endpoint);

            void _OnReceived(object sender, LinkEventArgs<LinkPacket> args) => RouteModule.Handle(args.Object);

            void _OnDisposed(object sender, LinkEventArgs<Exception> args)
            {
                clt.Received -= _OnReceived;
                clt.Disposed -= _OnDisposed;
                clt.Requested -= _ClientRequested;
                // 置空
                lock (s_ins._locker)
                    s_ins._client = null;
                var obj = args.Object;
                if (obj == null || obj is TaskCanceledException)
                    return;
                Entrance.ShowError("连接中断", obj);
            }

            clt.Received += _OnReceived;
            clt.Disposed += _OnDisposed;
            clt.Requested += _ClientRequested;

            lock (s_ins._locker)
            {
                if (s_ins._client != null)
                {
                    clt.Dispose();
                    throw new InvalidOperationException();
                }
                s_ins._client = clt;
            }

            HistoryModule.Handled += _HistoryHandled;
            ShareModule.PendingList.ListChanged += _PendingListChanged;

            ProfileModule.SetId(id);

            PostModule.UserProfile(Links.Id);
            PostModule.UserRequest();
            PostModule.UserGroups();

            clt.Start();
        }

        private static void _ClientRequested(object sender, LinkEventArgs<Socket> e)
        {
            var soc = e.Object;
            var buf = soc.ReceiveAsyncExt().WaitTimeout("Timeout when accept transport header."); var rea = new PacketReader(buf);
            var key = rea["data"].Pull<Guid>();
            var src = rea["source"].Pull<int>();

            Share.Notify(src, key, soc)?.Wait();
        }

        [Loader(0, LoaderFlags.OnExit)]
        public static void Shutdown()
        {
            lock (s_ins._locker)
            {
                s_ins._client?.Dispose();
                s_ins._client = null;
            }

            ShareModule.Close();
            ProfileModule.Clear();
            HistoryModule.Handled -= _HistoryHandled;
            ShareModule.PendingList.ListChanged -= _PendingListChanged;
        }

        public static void Enqueue(byte[] buffer) => s_ins._client?.Enqueue(buffer);

        /// <summary>
        /// 获取与连接关联的 NAT 内部端点和外部端点 (二者相同时只返回一个, 连接无效时返回空列表, 始终不会返回 null)
        /// </summary>
        public static List<IPEndPoint> GetEndPoints()
        {
            var lst = new List<IPEndPoint>();
            if (Extension.Lock(s_ins._locker, ref s_ins._client, out var clt) == false)
                return lst;
            lst.Add(clt.InnerEndPoint);
            lst.Add(clt.OuterEndPoint);
            var res = lst.Distinct().ToList();
            return res;
        }

        private static void _HistoryHandled(object sender, LinkEventArgs<Packet> e)
        {
            var hdl = new WindowInteropHelper(Application.Current.MainWindow).Handle;
            if (e.Finish == false || Application.Current.MainWindow.IsActive == false)
                NativeMethods.FlashWindow(hdl, true);
            return;
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

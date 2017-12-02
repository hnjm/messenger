using Messenger.Extensions;
using Messenger.Models;
using Mikodev.Logger;
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
        private readonly object _locker = new object();

        private LinkClient _client = null;

        /// <summary>
        /// 监听反向连接 (用于文件传输)
        /// </summary>
        private Socket _socket = null;

        private static readonly LinkModule s_ins = new LinkModule();

        public static int ID => s_ins._client?.ID ?? ProfileModule.Current.ID;

        public static bool IsRunning => s_ins._client?.IsRunning ?? false;

        /// <summary>
        /// 启动连接 (与 <see cref="Shutdown"/> 方法为非完全线程安全的关系, 不过两个方法不可能同时调用)
        /// </summary>
        public static void Start(int id, IPEndPoint endpoint)
        {
            var clt = new LinkClient(id);

            void _OnReceived(object sender, LinkEventArgs<LinkPacket> args) => RouteModule.Handle(args.Object);

            void _OnShutdown(object sender, LinkEventArgs<Exception> args)
            {
                clt.Received -= _OnReceived;
                clt.Shutdown -= _OnShutdown;
                // 置空
                lock (s_ins._locker)
                    s_ins._client = null;
                Entrance.ShowError("连接中断", args.Object);
            }

            clt.Received += _OnReceived;
            clt.Shutdown += _OnShutdown;

            var soc = new Socket(SocketType.Stream, ProtocolType.Tcp);
            soc.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            try
            {
                clt.Start(endpoint);
                soc.Bind(clt.InnerEndPoint);
                soc.Listen(Links.ClientCountLimit);
                lock (s_ins._locker)
                {
                    lock (s_ins._locker)
                        if (s_ins._client != null || s_ins._socket != null)
                            throw new InvalidOperationException();
                    s_ins._client = clt;
                    s_ins._socket = soc;
                }
            }
            catch (Exception)
            {
                clt.Dispose();
                soc.Dispose();
                throw;
            }

            _Listen(soc).ContinueWith(tsk => Log.Error(tsk.Exception));

            HistoryModule.Handled += _OnHistoryHandled;
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

        [Loader(0, LoaderFlags.OnExit)]
        public static void Shutdown()
        {
            lock (s_ins._locker)
            {
                s_ins._client?.Dispose();
                s_ins._client = null;
                s_ins._socket?.Dispose();
                s_ins._socket = null;
            }

            ShareModule.Close();
            ProfileModule.Clear();
            HistoryModule.Handled -= _OnHistoryHandled;
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

        private static void _OnHistoryHandled(object sender, LinkEventArgs<Packet> e)
        {
            var pro = ProfileModule.Query(e.Object.Groups);
            if (pro == null)
                return;
            var hdl = new WindowInteropHelper(Application.Current.MainWindow).Handle;
            if (e.Finish == false || Application.Current.MainWindow.IsActive == false)
                NativeMethod.FlashWindow(hdl, true);
            if (e.Finish == false || e.Cancel == true)
                pro.Hint += 1;
            return;
        }

        private static void _PendingListChanged(object sender, ListChangedEventArgs e)
        {
            if (sender == ShareModule.PendingList && e.ListChangedType == ListChangedType.ItemAdded)
            {
                if (Application.Current.MainWindow.IsActive == true)
                    return;
                var hdl = new WindowInteropHelper(Application.Current.MainWindow).Handle;
                NativeMethod.FlashWindow(hdl, true);
            }
        }
    }
}

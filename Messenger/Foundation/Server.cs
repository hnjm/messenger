using Messenger.Foundation.Extensions;
using Mikodev.Network;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Messenger.Foundation
{
    /// <summary>
    /// 消息服务器 (线程安全)
    /// </summary>
    public class Server : Manageable
    {
        /// <summary>
        /// 协议字符串
        /// </summary>
        public const string Protocol = "mikodev.messenger";
        /// <summary>
        /// 默认监听端口
        /// </summary>
        public const int DefaultPort = 7550;
        /// <summary>
        /// 默认连接响应超时
        /// </summary>
        public const int DefaultTimeout = 5 * 1000;
        /// <summary>
        /// KeepAlive 首次探测等待时间
        /// </summary>
        public const int DefaultKeepBefore = 20 * 1000;
        /// <summary>
        /// KeepAlive 探测间隔
        /// </summary>
        public const int DefaultKeepInterval = 1000;
        /// <summary>
        /// 默认连接限制
        /// </summary>
        public const int DefaultCountLimited = 32;
        /// <summary>
        /// 限制的最大连接数
        /// </summary>
        public const int DefaultCountOriginal = 256;
        /// <summary>
        /// 服务器编号 (正数为用户编号 负数为组编号)
        /// </summary>
        public const int ID = 0;

        /// <summary>
        /// 现有连接数
        /// </summary>
        public int Count => _clients?.Count ?? 0;
        /// <summary>
        /// 最大连接数
        /// </summary>
        public int CountLimited { get; private set; } = 0;
        /// <summary>
        /// 连接数变更事件 (后台执行)
        /// </summary>
        public event EventHandler CountChanged;

        private Socket _socket = null;
        private Thread _thread = null;
        /// <summary>
        /// 服务器广播调用链
        /// </summary>
        private EventHandler<GenericEventArgs<byte[]>> _srvbroa = null;
        /// <summary>
        /// 客户端列表
        /// </summary>
        private Dictionary<int, Client> _clients = new Dictionary<int, Client>();
        /// <summary>
        /// 客户端监听的组列表
        /// </summary>
        private Dictionary<int, List<int>> _groupsc = new Dictionary<int, List<int>>();

        /// <summary>
        /// 启动服务器 监听本地所有 IP 地址 (含 lock 语句)
        /// </summary>
        /// <param name="port">端口</param>
        /// <param name="max">最大连接数</param>
        public void Start(int port = DefaultPort, int max = DefaultCountLimited)
        {
            if (max < 1 || max > DefaultCountOriginal)
                throw new ArgumentOutOfRangeException(nameof(max), $"服务器最大连接数被限制在 {1} 和 {DefaultCountOriginal} 之间");

            lock (_locker)
            {
                if (IsStarted || IsDisposed)
                    throw new InvalidOperationException();
                _started = true;
            }

            var soc = default(Socket);
            var dis = new Action(() =>
                {
                    soc?.Dispose();
                    soc = null;
                });

            try
            {
                soc = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                soc.Bind(new IPEndPoint(IPAddress.Any, port));
                soc.Listen(max);
            }
            catch
            {
                dis.Invoke();
                throw;
            }

            lock (_locker)
            {
                if (IsDisposed)
                {
                    dis.Invoke();
                    throw new InvalidOperationException();
                }

                CountLimited = max;
                _socket = soc;
                _thread = new Thread(_Listen);
                _thread.Start();
            }
        }

        /// <summary>
        /// 执行 <see cref="Dispose(bool)"/> 并断开与所有客户端的连接 (不会触发 <see cref="CountChanged"/> 事件 含 lock 语句)
        /// </summary>
        public void Shutdown()
        {
            var lst = default(Client[]);
            lock (_locker)
            {
                Dispose(true);
                lst = new Client[_clients.Count];
                _clients.Values.CopyTo(lst, 0);
            }
            foreach (var c in lst)
                c.Dispose();
            return;
        }

        /// <summary>
        /// 触发 <see cref="CountChanged"/> 事件 (后台执行)
        /// </summary>
        private void _OnCountChanged() => Task.Run(() => CountChanged?.Invoke(this, new EventArgs()));

        /// <summary>
        /// 客户端传入连接监听线程
        /// </summary>
        private void _Listen()
        {
            while (_socket != null)
            {
                var soc = default(Socket);
                try
                {
                    soc = _socket.Accept();
                    soc.SetKeepAlive(true, DefaultKeepBefore, DefaultKeepInterval);
                }
                catch (Exception ex)
                {
                    if (soc != null)
                        soc.Close();
                    Trace.WriteLine(ex);
                    continue;
                }
                // 后台处理新连接 并负责释放出错的连接
                Task.Run(() =>
                    {
                        try
                        {
                            _CheckClient(soc);
                        }
                        catch (Exception ex)
                        {
                            Trace.WriteLine(ex);
                            soc.Dispose();
                        }
                    });
            }
        }

        private void _CheckClient(Socket client)
        {
            // 检查编号是否冲突
            ErrorCode check(int id)
            {
                lock (_locker)
                {
                    if (IsDisposed)
                        return ErrorCode.Shutdown;
                    if (_clients.ContainsKey(id))
                        return ErrorCode.Conflict;
                    // 空值做占位符
                    _clients.Add(id, null);
                    return ErrorCode.Success;
                }
            }
            // 移除占位符
            void remove(int id)
            {
                lock (_locker)
                {
                    if (_clients.TryGetValue(id, out var val) && val == null)
                        _clients.Remove(id);
                    else throw new ApplicationException("Failed to remove placeholder.");
                }
            }

            var err = ErrorCode.None;
            var buf = default(byte[]);

            Extension.TimeoutInvoke(() => buf = client.ReceiveExt(), DefaultTimeout);
            var rea = new PacketReader(buf);
            var req = new
            {
                id = rea["id"].Pull<int>(),
                rsakey = rea["rsakey"].Pull<string>(),
                protocol = rea["protocol"].Pull<string>(),
            };

            if (Protocol.Equals(req.protocol) == false)
                throw new ApplicationException("Protocol not match.");
            if (_clients.Count >= CountLimited)
                err = ErrorCode.Filled;
            else if (req.id <= ID)
                err = ErrorCode.Invalid;
            else
                err = check(req.id);

            var aes = new AesManaged();

            try
            {
                var rsa = new RSACryptoServiceProvider();
                rsa.FromXmlString(req.rsakey);
                var tmp = PacketWriter.Serialize(new Dictionary<string, object>()
                {
                    ["result"] = err,
                    ["aeskey"] = rsa.Encrypt(aes.Key, true),
                    ["aesiv"] = rsa.Encrypt(aes.IV, true),
                    ["endpoint"] = (IPEndPoint)client.RemoteEndPoint,
                });
                Extension.TimeoutInvoke(() => client.SendExt(tmp.GetBytes()), DefaultTimeout);
            }
            catch
            {
                if (err == ErrorCode.Success)
                    remove(req.id);
                throw;
            }

            if (err != ErrorCode.Success)
                throw new ConnectException(err);

            var clt = new Client(req.id);
            clt.Received += Client_Received;
            clt.Shutdown += Client_Shutdown;
            clt.Crypto = aes;

            // 调用 Shutdown 之前未被处理的连接在此断开
            lock (_locker)
            {
                remove(req.id);
                if (IsDisposed)
                    throw new ApplicationException("Server has been disposed.");
                _clients.Add(req.id, clt);
                _groupsc.Add(req.id, new List<int>());
                _srvbroa += clt.Enqueue;
                _OnCountChanged();
            }

            // 先加入列表再启动 避免客户端查找出现空值
            // 若在调用 Start 前被 Dispose, 异常会被上层捕获并关闭该套接字
            clt.Start(client);
        }

        /// <summary>
        /// 客户端关闭事件处理函数
        /// </summary>
        /// <param name="sender">发送事件的客户端实例</param>
        /// <param name="e">表示关闭原因事件参数</param>
        private void Client_Shutdown(object sender, EventArgs e)
        {
            var clt = (Client)sender;
            var idl = new List<int>();
            lock (_locker)
            {
                _clients.Remove(clt.ID);
                _groupsc.Remove(clt.ID);
                _srvbroa -= clt.Enqueue;
                if (IsDisposed)
                    return;
                foreach (var c in _clients)
                    idl.Add(c.Key);
                _OnCountChanged();
            }
            var buf = PacketWriter.Serialize(new
            {
                source = ID,
                target = ID,
                path = "user.ids",
                data = idl,
            });
            _srvbroa?.Invoke(this, new GenericEventArgs<byte[]>() { Source = this, Value = buf.GetBytes() });
        }

        /// <summary>
        /// 根据 ID 决定处理或转发客户端发来的消息
        /// </summary>
        private void Client_Received(object sender, GenericEventArgs<byte[]> arg)
        {
            var rea = new PacketReader(arg.Value);
            var src = rea["source"].Pull<int>();
            var tar = rea["target"].Pull<int>();
            var pth = rea["path"].Pull<string>();

            if (tar == ID && pth == "user.groups")
            {
                var lst = rea["data"].PullList<int>().ToList();
                lst.RemoveAll(r => r < ID == false);
                lock (_locker)
                {
                    _groupsc[src].Clear();
                    _groupsc[src] = lst;
                }
            }
            else if (tar == ID)
            {
                _srvbroa?.Invoke(this, arg);
            }
            else if (tar > ID)
            {
                _clients[tar].Enqueue(arg.Value);
            }
            else
            {
                lock (_locker)
                {
                    foreach (var (k, v) in _groupsc)
                    {
                        if (k == src)
                            continue;
                        if (v.Contains(tar))
                            _clients[k].Enqueue(arg.Value);
                    }
                }
            }
        }

        #region 实现 IDisposable
        /// <summary>
        /// 释放资源 (不含 lock 语句)
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            _socket?.Dispose();
            _socket = null;
            _disposed = true;
        }
        #endregion
    }
}

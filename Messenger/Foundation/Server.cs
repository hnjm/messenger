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
        public const int DefaultCountLimit = 128;
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

        private Socket _socket = null;
        private Thread _thread = null;
        /// <summary>
        /// 服务器广播调用链
        /// </summary>
        private EventHandler<LinkOldEventArgs<Router>> _srvbroa = null;
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
        public void Start(int port = DefaultPort, int max = DefaultCountLimit)
        {
            if (max < 1 || max > DefaultCountOriginal)
                throw new ArgumentOutOfRangeException(nameof(max), $"The maximum count should between {1} and {DefaultCountOriginal}.");

            lock (_loc)
            {
                if (IsStarted || IsDisposed)
                    throw new InvalidOperationException();
                _started = true;
            }

            var soc = default(Socket);
            void close()
            {
                soc?.Dispose();
                soc = null;
            }

            Extension.Invoke(() =>
            {
                soc = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                soc.Bind(new IPEndPoint(IPAddress.Any, port));
                soc.Listen(max);
            }, () => close());

            lock (_loc)
            {
                if (_disposed)
                {
                    close();
                    throw new InvalidOperationException();
                }

                CountLimited = max;
                _socket = soc;
                _thread = new Thread(_Listen);
                _thread.Start();
            }
        }

        /// <summary>
        /// 执行 <see cref="Dispose(bool)"/> 并断开与所有客户端的连接 (含 lock 语句)
        /// </summary>
        public void Shutdown()
        {
            var lst = default(Client[]);
            lock (_loc)
            {
                Dispose(true);
                lst = new Client[_clients.Count];
                _clients.Values.CopyTo(lst, 0);
            }
            foreach (var c in lst)
                c.Dispose();
            return;
        }

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
                    soc?.Dispose();
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
                lock (_loc)
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
                lock (_loc)
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

            Extension.Invoke(() =>
            {
                var rsa = new RSACryptoServiceProvider();
                rsa.FromXmlString(req.rsakey);
                var tmp = PacketWriter.Serialize(new
                {
                    result = err,
                    aeskey = rsa.Encrypt(aes.Key, true),
                    aesiv = rsa.Encrypt(aes.IV, true),
                    endpoint = (IPEndPoint)client.RemoteEndPoint,
                });
                Extension.TimeoutInvoke(() => client.SendExt(tmp.GetBytes()), DefaultTimeout);
            },
            () =>
            {
                if (err != ErrorCode.Success)
                    return;
                remove(req.id);
            });

            if (err != ErrorCode.Success)
                throw new LinkOldException(err);

            var clt = new Client(req.id);
            clt.Received += Client_Received;
            clt.Shutdown += Client_Shutdown;
            clt.Crypto = aes;

            // 调用 Shutdown 之前未被处理的连接在此断开
            lock (_loc)
            {
                remove(req.id);
                if (_disposed)
                    throw new ApplicationException("Server has been disposed.");
                _clients.Add(req.id, clt);
                _groupsc.Add(req.id, new List<int>());
                _srvbroa += clt.Enqueue;
            }

            // 先加入列表再启动 避免客户端查找出现空值
            // 若在调用 Start 前被 Dispose, 异常会被上层捕获并关闭该套接字
            clt.Start(client);
        }

        private void Client_Shutdown(object sender, EventArgs e)
        {
            var clt = (Client)sender;
            var idl = new List<int>();
            lock (_loc)
            {
                _clients.Remove(clt.ID);
                _groupsc.Remove(clt.ID);
                _srvbroa -= clt.Enqueue;
                if (IsDisposed)
                    return;
                foreach (var c in _clients)
                    idl.Add(c.Key);
            }
            var buf = PacketWriter.Serialize(new
            {
                source = ID,
                target = ID,
                path = "user.ids",
                data = idl,
            });
            _srvbroa?.Invoke(this, new LinkOldEventArgs<Router>() { Source = this, Record = new Router().Load(buf.GetBytes()) });
        }

        private void Client_Received(object sender, LinkOldEventArgs<Router> arg)
        {
            var rea = arg.Record;
            var src = rea.Source;
            var tar = rea.Target;
            var pth = rea.Path;

            if (tar == ID)
            {
                if (pth == "user.groups")
                {
                    var lst = rea.Data.PullList<int>().ToList();
                    lst.RemoveAll(r => r < ID == false);
                    lock (_loc)
                    {
                        _groupsc[src].Clear();
                        _groupsc[src] = lst;
                    }
                    return;
                }
                _srvbroa?.Invoke(this, arg);
            }
            else if (tar > ID)
            {
                _clients[tar].Enqueue(rea.Buffer);
            }
            else
            {
                lock (_loc)
                {
                    foreach (var (k, v) in _groupsc)
                    {
                        if (k == src)
                            continue;
                        if (v.Contains(tar))
                            _clients[k].Enqueue(rea.Buffer);
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

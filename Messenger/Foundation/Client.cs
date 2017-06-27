using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;

namespace Messenger.Foundation
{
    /// <summary>
    /// 客户端 (线程安全)
    /// </summary>
    public class Client : Manageable
    {
        /// <summary>
        /// 最大同时接受传输请求数
        /// </summary>
        public const int DefaultRequest = 16;

        /// <summary>
        /// 编号
        /// </summary>
        public int ID { get; private set; } = 0;
        /// <summary>
        /// 消息接收事件
        /// </summary>
        public event EventHandler<PacketEventArgs> Received = null;
        /// <summary>
        /// 传输请求事件
        /// </summary>
        public event EventHandler<GenericEventArgs<(Guid, Socket)>> Requests = null;
        /// <summary>
        /// 连接关闭事件
        /// </summary>
        public event EventHandler Shutdown = null;
        /// <summary>
        /// 加密解密类
        /// </summary>
        public AesManaged Crypto { get; set; } = null;
        /// <summary>
        /// 获取触发 <see cref="Shutdown"/> 事件的异常对象
        /// </summary>
        public Exception Exception { get; private set; } = null;
        /// <summary>
        /// 获取本机连接端点
        /// </summary>
        public EndPoint InnerEndPoint => _socket?.LocalEndPoint;
        /// <summary>
        /// 获取本机相对于服务器的 NAT 外部端点
        /// </summary>
        public EndPoint OuterEndPoint { get; private set; } = null;

        private ConcurrentQueue<byte[]> _messages = new ConcurrentQueue<byte[]>();
        private Socket _socket = null;
        private Socket _ftrans = null;
        private Thread _recvth = null;
        private Thread _sendth = null;
        private Thread _lstnth = null;

        /// <summary>
        /// 初始化客户端实例
        /// </summary>
        /// <param name="id">自定义 ID</param>
        public Client(int id) => ID = id;

        /// <summary>
        /// 使用指定套接字启动连接 (服务器端使用 含 lock 语句)
        /// </summary>
        public void Start(Socket client)
        {
            lock (_locker)
            {
                if (_started || _disposed)
                    throw new InvalidOperationException();
                _started = true;

                _socket = client;
                _sendth = new Thread(_Maker);
                _recvth = new Thread(_Taker);
                _sendth.Start();
                _recvth.Start();
            }
        }

        /// <summary>
        /// 使用指定地址启动连接 (客户端使用 含 lock 语句)
        /// </summary>
        public void Start(IPEndPoint ep)
        {
            lock (_locker)
            {
                if (_started || _disposed)
                    throw new InvalidOperationException();
                _started = true;
            }

            var buf = default(byte[]);
            var aes = default(AesManaged);
            var iep = default(IPEndPoint);
            var res = default(PacketRespond);
            var rsa = new RSACryptoServiceProvider(1024);
            var req = new PacketRequest() { ID = ID, Protocol = Server.Protocol, RsaKey = rsa.ToXmlString(false) };

            var soc = default(Socket);
            var tra = default(Socket);
            var dis = new Action(() =>
                {
                    soc?.Dispose();
                    soc = null;
                    tra?.Dispose();
                    tra = null;
                });

            try
            {
                soc = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                Extension.TimeoutInvoke(() => soc.Connect(ep), Server.DefaultTimeout);
                soc.SetKeepAlive(true, Server.DefaultKeepBefore, Server.DefaultKeepInterval);
                // 使用明文发送用户信息和 RSA 公钥
                buf = Xml.Serialize(req);
                Extension.TimeoutInvoke(() => soc.SendExt(buf), Server.DefaultTimeout);
                // 接收服务器返回值
                Extension.TimeoutInvoke(() => buf = soc.ReceiveExt(), Server.DefaultTimeout);
                res = Xml.Deserialize<PacketRespond>(buf);
                if (res?.Result != ErrorCode.Success)
                    throw new ConnectException(res?.Result ?? ErrorCode.InnerError);
                // 监听套接字绑定
                tra = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                tra.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                tra.Bind(soc.LocalEndPoint);
                tra.Listen(DefaultRequest);

                iep = res.EndPoint.ToEndPoint();
                aes = new AesManaged() { Key = rsa.Decrypt(res.AesKey, true), IV = rsa.Decrypt(res.AesIV, true) };
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

                Crypto = aes;
                OuterEndPoint = iep;
                _socket = soc;
                _ftrans = tra;
                // 除了读写线程外 还有一个监听线程用于与其他客户端建立文件传输
                _sendth = new Thread(_Maker);
                _recvth = new Thread(_Taker);
                _lstnth = new Thread(_Listen);
                _sendth.Start();
                _recvth.Start();
                _lstnth.Start();
            }
        }

        /// <summary>
        /// 向待发队列尾插入一条消息
        /// </summary>
        /// <exception cref="ArgumentNullException"></exception>
        public void Enqueue(byte[] msg)
        {
            if (msg == null)
                throw new ArgumentNullException(nameof(msg));
            _messages.Enqueue(msg);
        }

        /// <summary>
        /// 向待发队列尾插入一条消息
        /// </summary>
        public void Enqueue(object sender, PacketEventArgs e)
        {
            if (e.Source == ID && e.Buffer != null)
                return;
            _messages.Enqueue(e.Buffer);
        }

        /// <summary>
        /// 套接字接收线程
        /// </summary>
        private void _Taker()
        {
            try
            {
                while (_socket != null)
                {
                    var dst = Crypto.Decrypt(_socket.ReceiveExt());
                    _OnReceived(new PacketEventArgs(dst));
                }
            }
            catch (Exception ex)
            {
                Log.E(nameof(Client), ex, "接收线程异常.");
                _OnShutdown(ex);
            }
        }

        private void _Maker()
        {
            try
            {
                while (_socket != null)
                {
                    if (_messages.TryDequeue(out var bts))
                        _socket.SendExt(Crypto.Encrypt(bts));
                    else
                        Thread.Sleep(1);
                }
            }
            catch (Exception ex)
            {
                Log.E(nameof(Client), ex, "发送线程异常.");
                _OnShutdown(ex);
            }
        }

        private void _Listen()
        {
            while (_ftrans != null)
            {
                var soc = default(Socket);
                try
                {
                    soc = _ftrans.Accept();
                    soc.SetKeepAlive(true, Server.DefaultKeepBefore, Server.DefaultKeepInterval);
                    var buf = soc.ReceiveExt();
                    var key = Xml.Deserialize<Guid>(buf);
                    var req = new GenericEventArgs<(Guid, Socket)>() { Value = (key, soc) };
                    Requests?.Invoke(this, req);
                    if (req.Handled == false)
                        soc.Dispose();
                }
                catch (Exception ex)
                {
                    if (soc != null)
                        soc.Dispose();
                    Log.E(nameof(Client), ex, "监听线程异常.");
                    continue;
                }
            }
        }

        /// <summary>
        /// 内部信息处理函数 解析 ID 和控制字符串 并决定是否向上发送事件
        /// </summary>
        private void _OnReceived(PacketEventArgs arg)
        {
            try
            {
                // 拦截连接控制事件
                if (arg.Source == Server.ID && arg.Genre == PacketGenre.LinkShutdown)
                    _OnShutdown();
                else
                    Received?.Invoke(this, arg);
            }
            catch (Exception ex)
            {
                Log.E(nameof(Client), ex, "消息处理出错.");
            }
        }

        /// <summary>
        /// 停止客户端 并触发客户端关闭事件
        /// </summary>
        private void _OnShutdown(Exception ex = null)
        {
            lock (_locker)
            {
                if (_disposed)
                    return;
                Dispose(true);
                Exception = ex;
                Shutdown?.Invoke(this, new EventArgs());
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
            _ftrans?.Dispose();
            _ftrans = null;
            _disposed = true;
        }
        #endregion
    }
}

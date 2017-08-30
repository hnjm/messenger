﻿using Messenger.Foundation.Extensions;
using Mikodev.Network;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
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
        public event EventHandler<LinkOldEventArgs<Router>> Received = null;
        /// <summary>
        /// 传输请求事件
        /// </summary>
        public event EventHandler<LinkOldEventArgs<(Guid, Socket)>> Requests = null;
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
            lock (_loc)
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
            lock (_loc)
            {
                if (_started || _disposed)
                    throw new InvalidOperationException();
                _started = true;
            }

            var rsa = new RSACryptoServiceProvider();
            var iep = default(IPEndPoint);
            var aes = default(AesManaged);
            var req = PacketWriter.Serialize(new
            {
                id = ID,
                protocol = Server.Protocol,
                rsakey = rsa.ToXmlString(false),
            });

            var soc = default(Socket);
            var tra = default(Socket);
            void close()
            {
                soc?.Dispose();
                soc = null;
                tra?.Dispose();
                tra = null;
            }

            Extension.Invoke(() =>
            {
                soc = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                Extension.TimeoutInvoke(() => soc.Connect(ep), Server.DefaultTimeout);
                soc.SetKeepAlive(true, Server.DefaultKeepBefore, Server.DefaultKeepInterval);
                Extension.TimeoutInvoke(() => soc.SendExt(req.GetBytes()), Server.DefaultTimeout);
                var buf = default(byte[]);
                Extension.TimeoutInvoke(() => buf = soc.ReceiveExt(), Server.DefaultTimeout);
                var rea = new PacketReader(buf);
                var res = new
                {
                    result = rea["result"].Pull<ErrorCode>(),
                    aeskey = rea["aeskey"].PullList(),
                    aesiv = rea["aesiv"].PullList(),
                    endpoint = rea["endpoint"].Pull<IPEndPoint>(),
                };
                if (res.result != ErrorCode.Success)
                    throw new LinkOldException(res.result);
                tra = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                tra.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                tra.Bind(soc.LocalEndPoint);
                tra.Listen(DefaultRequest);

                iep = res.endpoint;
                aes = new AesManaged() { Key = rsa.Decrypt(res.aeskey, true), IV = rsa.Decrypt(res.aesiv, true) };
            }, () => close());

            lock (_loc)
            {
                if (_disposed)
                {
                    close();
                    throw new InvalidOperationException();
                }

                Crypto = aes;
                OuterEndPoint = iep;
                _socket = soc;
                _ftrans = tra;
                // more one thread for file transform
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
        public void Enqueue(object sender, LinkOldEventArgs<Router> e)
        {
            var rcd = e.Record;
            if (rcd.Source == ID)
                return;
            _messages.Enqueue(rcd.Buffer);
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
                    var buf = _socket.ReceiveExt();
                    var dst = Crypto.Decrypt(buf);
                    _OnReceived(new LinkOldEventArgs<Router>() { Source = this, Record = new Router().Load(dst) });
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
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
                Trace.WriteLine(ex);
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
                    var key = new PacketReader(buf).Pull<Guid>();
                    var req = new LinkOldEventArgs<(Guid, Socket)>() { Record = (key, soc) };
                    Requests?.Invoke(this, req);
                    if (req.Finish == false)
                        soc.Dispose();
                }
                catch (Exception ex)
                {
                    soc?.Dispose();
                    Trace.WriteLine(ex);
                    continue;
                }
            }
        }

        /// <summary>
        /// 内部信息处理函数 解析 ID 和控制字符串 并决定是否向上发送事件
        /// </summary>
        private void _OnReceived(LinkOldEventArgs<Router> arg)
        {
            try
            {
                var rcd = arg.Record;
                // 拦截连接控制事件
                if (rcd.Source == Server.ID && rcd.Path == "link.shutdown")
                    _OnShutdown();
                else
                    Received?.Invoke(this, arg);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
            }
        }

        /// <summary>
        /// 停止客户端 并触发客户端关闭事件
        /// </summary>
        private void _OnShutdown(Exception ex = null)
        {
            lock (_loc)
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

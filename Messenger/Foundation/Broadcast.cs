using Messenger.Foundation.Extensions;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Messenger.Foundation
{
    /// <summary>
    /// UDP 广播 (线程安全)
    /// </summary>
    public class Broadcast : Manageable
    {
        /// <summary>
        /// 默认广播端口
        /// </summary>
        public const int DefaultPort = 58976;

        /// <summary>
        /// 监听端口
        /// </summary>
        public int Port { get; set; } = DefaultPort;
        /// <summary>
        /// 报文处理函数
        /// </summary>
        public Func<byte[], byte[]> Function { get; set; } = null;

        private Socket _socket = null;
        private Thread _listen = null;

        /// <summary>
        /// 在后台线程上持续监听广播
        /// </summary>
        public override void Start()
        {
            lock (_locker)
            {
                if (_started || _disposed)
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
                soc = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                soc.Bind(new IPEndPoint(IPAddress.Any, Port));
            }, () => close());

            lock (_locker)
            {
                if (_disposed)
                {
                    close();
                    throw new InvalidOperationException();
                }

                _socket = soc;
                _listen = new Thread(_Listen);
                _listen.Start();
            }
        }

        /// <summary>
        /// 循环监听客户端广播并回应
        /// </summary>
        private void _Listen()
        {
            while (_socket != null)
            {
                try
                {
                    var ava = _socket.Available;
                    if (ava < 1)
                    {
                        Thread.Sleep(1);
                        continue;
                    }
                    var buf = new byte[ava];
                    var iep = new IPEndPoint(IPAddress.Any, IPEndPoint.MinPort) as EndPoint;
                    _socket.ReceiveFrom(buf, ref iep);
                    var val = Function.Invoke(buf);
                    if (val == null)
                        continue;
                    _socket.SendTo(val, iep);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex);
                }
            }
        }

        #region 实现 IDisposable
        protected override void Dispose(bool disposing)
        {
            _socket?.Dispose();
            _socket = null;
            _disposed = true;
        }
        #endregion
    }
}

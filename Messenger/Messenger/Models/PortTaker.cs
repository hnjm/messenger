using Messenger.Extensions;
using Messenger.Modules;
using Mikodev.Logger;
using Mikodev.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Messenger.Models
{
    /// <summary>
    /// 文件接收类 (线程安全)
    /// </summary>
    public class PortTaker : Port
    {
        private CancellationTokenSource _cancel = new CancellationTokenSource();
        private Socket _socket = null;
        private List<IPEndPoint> _endpoints = null;

        /// <summary>
        /// 初始化对象 并设定文件保存路径函数
        /// </summary>
        public PortTaker(PacketReader reader)
        {
            if (reader == null)
                throw new ArgumentNullException();
            _key = reader["key"].Pull<Guid>();
            _name = reader["name"].Pull<string>();
            _length = reader["length"].Pull<long>();
            _endpoints = reader["endpoints"].PullList<IPEndPoint>().ToList();
            _status = PortStatus.等待;
        }

        /// <summary>
        /// 启动文件接收 失败时自动调用 <see cref="_Dispose(bool)"/>
        /// </summary>
        public Task Start()
        {
            lock (_loc)
            {
                if (_started || _disposed)
                    throw new InvalidOperationException();
                _started = true;

                _status = PortStatus.运行;
                _EmitStarted();
            }

            var soc = default(Socket);
            // 与发送者建立连接 (尝试连接对方返回的所有 IP, 原理请参考 "TCP NAT 穿透")
            async Task _Emit()
            {
                for (int i = 0; i < _endpoints.Count && soc == null; i++)
                {
                    try
                    {
                        soc = new Socket(SocketType.Stream, ProtocolType.Tcp);
                        soc.ConnectAsyncEx(_endpoints[i]).WaitTimeout("Port receiver timeout.");
                        soc.SetKeepAlive();
                        break;
                    }
                    catch (Exception ex) when (ex is SocketException || ex is TimeoutException)
                    {
                        soc.Dispose();
                        soc = null;
                        Log.Err(ex);
                    }
                }

                if (soc == null)
                    throw new ApplicationException("Network unreachable.");
                var buf = PacketWriter.Serialize(new
                {
                    data = _key,
                    source = Linkers.ID,
                });
                await soc.SendAsyncExt(buf.GetBytes());

                lock (_loc)
                {
                    if (_disposed)
                        throw new InvalidOperationException();
                    _socket = soc;
                }
            }

            // 在接收函数退出时设置状态并释放资源
            return _Emit().ContinueWith(t =>
            {
                if (t.Exception == null)
                {
                    _Receive().ContinueWith(_Clean);
                    return;
                }
                soc?.Dispose();
                Dispose();
            });
        }

        /// <summary>
        /// 接收文件, 并设置传输进度 (异步)
        /// </summary>
        private async Task _Receive()
        {
            if (_batch)
                throw new NotImplementedException();

            var inf = Ports.FindAvailablePath(_name);
            _name = inf.Name;
            await _socket.ReceiveFileEx(inf.FullName, _length, r => _position += r, _cancel.Token);
        }

        /// <summary>
        /// 清理资源, 若文件没有成功接收, 则删除该文件
        /// </summary>
        /// <param name="task"></param>
        private void _Clean(Task task)
        {
            lock (_loc)
            {
                if (_disposed)
                    return;
                var exc = task.Exception;
                _status = (exc == null) ? PortStatus.成功 : PortStatus.中断;
                _exception = exc;
                _Dispose();
            }
        }

        #region 实现 IDisposable
        /// <summary>
        /// 释放资源并在后台触发 <see cref="Port.Disposed"/> 事件 (不含 lock 语句)
        /// </summary>
        protected override void _Dispose()
        {
            if (_disposed)
                return;
            if ((_status & PortStatus.终止) == 0)
                _status = PortStatus.取消;

            _cancel.Cancel();
            _socket?.Dispose();
            _socket = null;

            _disposed = true;
            _EmitDisposed();
        }
        #endregion
    }
}

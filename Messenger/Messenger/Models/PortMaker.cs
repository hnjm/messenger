using Mikodev.Network;
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using Messenger.Extensions;
using System.Threading;

namespace Messenger.Models
{
    /// <summary>
    /// 文件发送类 (事件驱动 线程安全)
    /// </summary>
    public class PortMaker : Port
    {
        private readonly CancellationTokenSource _cancel = new CancellationTokenSource();
        private Socket _socket = null;
        private readonly string _path;

        /// <summary>
        /// 由事件触发 不可直接启动
        /// </summary>
        public override bool CanStart => false;

        /// <summary>
        /// 创建文件发送对象
        /// </summary>
        /// <param name="path">文件路径</param>
        public PortMaker(string path)
        {
            var inf = new FileInfo(path);
            _path = inf.FullName;
            _name = inf.Name;
            _length = inf.Length;
            _status = PortStatus.等待;
        }

        /// <summary>
        /// 处理传输请求
        /// </summary>
        public void PortRequests(object sender, LinkEventArgs<Guid> e)
        {
            var key = e.Record;
            var soc = e.Source as Socket;
            if (_key.Equals(key) == false || soc == null)
                return;
            e.Finish = true;

            lock (_loc)
            {
                if (_started || _disposed)
                    return;
                _started = true;
                _socket = soc;
                _status = PortStatus.运行;
            }

            _EmitStarted();
            soc.SendFileEx(_path, _length, r => _position += r, _cancel.Token).ContinueWith(_Clean);
        }

        internal void _Clean(Task task)
        {
            lock (_loc)
            {
                if (_disposed)
                    return;
                var exc = task.Exception;
                if (task.IsCanceled)
                    _status = PortStatus.取消;
                else if (exc != null)
                    _status = PortStatus.中断;
                else
                    _status = PortStatus.成功;
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

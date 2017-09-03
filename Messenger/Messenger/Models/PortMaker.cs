using Mikodev.Network;
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Messenger.Models
{
    /// <summary>
    /// 文件发送类 (事件驱动 线程安全)
    /// </summary>
    public class PortMaker : Port
    {
        private FileStream _stream = null;
        private Socket _socket = null;

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
            var inf = default(FileInfo);
            var str = default(FileStream);

            try
            {
                inf = new FileInfo(path);
                str = File.OpenRead(inf.FullName);
            }
            catch (Exception)
            {
                str?.Dispose();
                str = null;
            }

            _stream = str;
            _name = inf.Name;
            _length = str.Length;
            _status = PortStatus.等待;
        }

        /// <summary>
        /// 循环发送文件
        /// </summary>
        private async Task _Read()
        {
            while (_socket != null)
            {
                if (_position >= _length)
                    break;
                if (_stream.Position != _position)
                    _stream.Seek(_position, SeekOrigin.Begin);
                var len = (long)Links.Buffer;
                if (_length - _position < len)
                    len = _length - _position;
                var buf = new byte[len];
                var sub = await _stream.ReadAsync(buf, 0, buf.Length);
                if (sub < buf.Length)
                {
                    if (sub < 1)
                        throw new IOException("Read file error!");
                    var tmp = new byte[sub];
                    Buffer.BlockCopy(buf, 0, tmp, 0, sub);
                    buf = tmp;
                }
                await _socket._SendAsync(buf);
                _position += sub;
            }
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
            _Read().ContinueWith(t =>
            {
                var res = (t.Exception == null && _position == _length);
                lock (_loc)
                {
                    if (_disposed == false)
                    {
                        _status = res ? PortStatus.成功 : PortStatus.中断;
                        _exception = t.Exception;
                        _Dispose();
                    }
                }
            });
        }

        #region 实现 IDisposable
        /// <summary>
        /// 释放资源并在后台触发 <see cref="Port.Disposed"/> 事件 (不含 lock 语句)
        /// </summary>
        protected override void _Dispose()
        {
            if (_disposed)
                return;

            var val = _status & PortStatus.终止;
            if (val == 0)
                _status = PortStatus.取消;

            _socket?.Dispose();
            _socket = null;
            _stream?.Dispose();
            _stream = null;

            _disposed = true;
            _EmitDisposed();
        }
        #endregion
    }
}

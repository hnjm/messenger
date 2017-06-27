using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace Messenger.Foundation
{
    /// <summary>
    /// 文件发送类 (事件驱动 线程安全)
    /// </summary>
    public class Maker : Transport
    {
        private FileStream _stream = null;
        private Socket _socket = null;
        private Thread _thread = null;

        /// <summary>
        /// 由事件触发 不可直接启动
        /// </summary>
        public override bool CanStart => false;

        /// <summary>
        /// 创建文件发送对象
        /// </summary>
        /// <param name="path">文件路径</param>
        public Maker(string path)
        {
            var inf = default(FileInfo);
            var str = default(FileStream);
            var dis = new Action(() =>
                {
                    str?.Dispose();
                    str = null;
                });

            try
            {
                inf = new FileInfo(path);
                str = File.OpenRead(inf.FullName);
            }
            catch
            {
                dis.Invoke();
                throw;
            }

            _stream = str;
            _name = inf.Name;
            _length = str.Length;
            _status = TransportStatus.等待;
        }

        /// <summary>
        /// 循环发送文件
        /// </summary>
        private void _Maker()
        {
            var exc = default(Exception);
            try
            {
                while (_socket != null)
                {
                    if (_position >= _length)
                        break;
                    _stream.Seek(_position, SeekOrigin.Begin);
                    var len = (long)short.MaxValue;
                    if (_position + len > _length)
                        len = _length - _position;
                    var buf = new byte[len];
                    _stream.Read(buf, 0, buf.Length);
                    _socket.Send(buf);
                    _position += len;
                }
            }
            catch (Exception ex)
            {
                exc = ex;
            }

            var res = (exc == null && _position == _length);
            lock (_locker)
            {
                if (IsDisposed == false)
                {
                    _status = res ? TransportStatus.成功 : TransportStatus.中断;
                    _exception = exc;
                    Dispose(true);
                }
            }
        }

        /// <summary>
        /// 处理传输请求
        /// </summary>
        public void Transport_Requests(object sender, GenericEventArgs<(Guid, Socket)> e)
        {
            var (key, soc) = e.Value;
            if (_key.Equals(key) == false || soc == null)
                return;
            lock (_locker)
            {
                if (_started || _disposed)
                    return;
                _started = true;
                _socket = soc;
                e.Handled = true;

                _status = TransportStatus.运行;
                _OnStarted();
                _thread = new Thread(_Maker);
                _thread.Start();
            }
        }

        #region 实现 IDisposable
        /// <summary>
        /// 释放资源并在后台触发 <see cref="Transport.Disposed"/> 事件 (不含 lock 语句)
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            var val = _status & TransportStatus.终态;
            if (val == 0)
                _status = TransportStatus.取消;

            _socket?.Dispose();
            _socket = null;
            _stream?.Dispose();
            _stream = null;

            _disposed = true;
            _OnDisposed();
        }
        #endregion
    }
}

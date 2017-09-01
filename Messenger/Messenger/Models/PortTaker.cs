using Mikodev.Network;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Messenger.Models
{
    /// <summary>
    /// 文件接收类 (线程安全)
    /// </summary>
    public class PortTaker : Port
    {
        private string _path = null;
        private FileStream _stream = null;
        private Socket _socket = null;
        private List<IPEndPoint> _ieps = null;
        private Func<string> _callback = null;

        /// <summary>
        /// 初始化对象 并设定文件保存路径函数
        /// </summary>
        public PortTaker(PacketReader reader, Func<string> callback)
        {
            if (reader == null || callback == null)
                throw new ArgumentNullException();
            _key = reader["guid"].Pull<Guid>();
            _name = reader["filename"].Pull<string>();
            _ieps = reader["endpoints"].PullList<IPEndPoint>().ToList();
            _length = reader["filesize"].Pull<long>();
            _status = PortStatus.等待;
            _callback = callback;
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
            var inf = default(FileInfo);
            var str = default(FileStream);

            async Task _Emit()
            {
                for (int i = 0; i < _ieps.Count && soc == null; i++)
                {
                    try
                    {
                        soc = new Socket(SocketType.Stream, ProtocolType.Tcp);
                        if (Task.Run(() => soc.Connect(_ieps[i])).Wait(Links.Timeout) == false)
                            throw new TimeoutException("Port receiver timeout.");
                        soc._SetKeepAlive();
                        break;
                    }
                    catch (Exception ex) when (ex is SocketException || ex is TimeoutException)
                    {
                        soc.Dispose();
                        soc = null;
                        Trace.WriteLine(ex);
                    }
                }

                if (soc == null)
                    throw new ApplicationException("Network unreachable.");
                var buf = PacketWriter.Serialize(_key);
                await soc._SendExtendAsync(buf.GetBytes());
                inf = new FileInfo(_callback.Invoke());
                str = new FileStream(inf.FullName, FileMode.CreateNew);

                _name = inf.Name;
                _path = inf.FullName;

                lock (_loc)
                {
                    if (_disposed)
                        throw new InvalidOperationException();
                    _socket = soc;
                    _stream = str;
                }
            }

            return _Emit().ContinueWith(t =>
            {
                if (t.Exception == null)
                {
                    _Receive().ContinueWith(_ReceiveClean);
                    return;
                }
                soc?.Dispose();
                str?.Dispose();
                soc = null;
                str = null;
                Dispose();
            });
        }

        private async Task _Receive()
        {
            while (_socket != null && _position < _length)
            {
                if (_stream.Position != _position)
                    _stream.Seek(_position, SeekOrigin.Begin);
                var sub = (int)Math.Min(_length - _position, Links.Buffer);
                var buf = await _socket._ReceiveAsync(sub);
                _stream.Write(buf, 0, buf.Length);
                _position += sub;
            }
        }

        private void _ReceiveClean(Task task)
        {
            var res = (task.Exception == null && _position == _length);
            lock (_loc)
            {
                if (_disposed == false)
                {
                    _status = res ? PortStatus.成功 : PortStatus.中断;
                    if (task.Exception != null)
                        _exception = task.Exception;
                    _Dispose();
                }
            }

            if (res == true || File.Exists(_path) == false)
                return;

            try
            {
                File.Delete(_path);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
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

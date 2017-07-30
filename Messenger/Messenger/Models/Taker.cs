using Messenger.Foundation;
using Messenger.Foundation.Extensions;
using Mikodev.Network;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Messenger.Models
{
    /// <summary>
    /// 文件接收类 (线程安全)
    /// </summary>
    public class Taker : Transport
    {
        private string _path = null;
        private FileStream _stream = null;
        private Socket _socket = null;
        private Thread _thread = null;
        private List<IPEndPoint> _ieps = null;
        private Func<string> _callback = null;

        /// <summary>
        /// 初始化对象 并设定文件保存路径函数
        /// </summary>
        public Taker(PacketReader reader, Func<string> callback)
        {
            if (reader == null || callback == null)
                throw new ArgumentNullException();
            _key = reader["guid"].Pull<Guid>();
            _name = reader["filename"].Pull<string>();
            _ieps = reader["endpoints"].PullList<IPEndPoint>().ToList();
            _length = reader["filesize"].Pull<long>();
            _status = TransportStatus.等待;
            _callback = callback;
        }

        /// <summary>
        /// 启动文件接收 失败时自动调用 <see cref="Dispose(bool)"/>
        /// </summary>
        public override void Start()
        {
            lock (_loc)
            {
                if (_started || _disposed)
                    throw new InvalidOperationException();
                _started = true;

                _status = TransportStatus.运行;
                _OnStarted();
            }

            var soc = default(Socket);
            var inf = default(FileInfo);
            var str = default(FileStream);

            void _start()
            {
                for (int i = 0; i < _ieps.Count && soc == null; i++)
                {
                    try
                    {
                        soc = new Socket(SocketType.Stream, ProtocolType.Tcp);
                        Extension.TimeoutInvoke(() => soc.Connect(_ieps[i]), Server.DefaultTimeout);
                        soc.SetKeepAlive(true, Server.DefaultKeepBefore, Server.DefaultKeepInterval);
                    }
                    catch (Exception ex)
                    {
                        soc.Dispose();
                        soc = null;
                        Trace.WriteLine(ex);
                    }
                }

                if (soc == null)
                    throw new ApplicationException("Network is unreachable.");

                var buf = PacketWriter.Serialize(_key);
                soc.SendExt(buf.GetBytes());
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
                    _thread = new Thread(_Taker);
                    _thread.Start();
                }
            }

            Extension.Invoke(() => _start(), () =>
            {
                soc?.Dispose();
                str?.Dispose();
                soc = null;
                str = null;
                Dispose();
            });
        }

        /// <summary>
        /// 循环接收文件
        /// </summary>
        private void _Taker()
        {
            var exc = default(Exception);
            void _receive()
            {
                _stream.Seek(_position, SeekOrigin.Begin);
                var sub = (long)short.MaxValue;
                if (_position + sub > _length)
                    sub = _length - _position;
                var buf = new byte[sub];
                int len = _socket.Receive(buf, 0, (int)sub, SocketFlags.None, out var err);
                if (len < 1)
                    throw new SocketException((int)SocketError.ConnectionReset);
                _stream.Write(buf, 0, len);
                _position += len;
            }

            try
            {
                while (_socket != null && _position < _length)
                    _receive();
            }
            catch (Exception e)
            {
                exc = e;
            }

            var res = (exc == null && _position == _length);
            lock (_loc)
            {
                if (_disposed == false)
                {
                    _status = res ? TransportStatus.成功 : TransportStatus.中断;
                    _exception = exc;
                    Dispose(true);
                }
            }

            try
            {
                if (res == false && File.Exists(_path))
                    File.Delete(_path);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
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

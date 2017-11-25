using Messenger.Extensions;
using Messenger.Modules;
using Mikodev.Logger;
using Mikodev.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Messenger.Models
{
    public class ShareReceiver : ShareBasic, IDisposed
    {
        private readonly object _locker = new object();
        private readonly CancellationTokenSource _cancel = new CancellationTokenSource();

        internal readonly int _id;
        internal readonly Guid _key;
        internal readonly long _length;
        internal readonly bool _batch = false;

        internal bool _started = false;
        internal bool _disposed = false;
        internal long _position = 0;
        internal string _name = null;
        internal string _path = null;
        internal ShareStatus _status;

        private Socket _socket = null;
        private List<IPEndPoint> _endpoints = null;

        public bool IsStarted => _started;

        public override long Length => _length;

        public override bool IsBatch => _batch;

        public override bool IsDisposed => _disposed;

        public override string Name => _name;

        public override string Path => _path;

        public override long Position => _position;

        public override ShareStatus Status => _status;

        protected override int ID => _id;

        public ShareReceiver(int id, PacketReader reader)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            _id = id;
            var typ = reader["type"].Pull<string>();
            if (typ == "file")
                _length = reader["length"].Pull<long>();
            else if (typ == "dir")
                _batch = true;
            else
                throw new ApplicationException("Invalid share type!");

            _key = reader["key"].Pull<Guid>();
            _name = reader["name"].Pull<string>();
            _endpoints = reader["endpoints"].PullList<IPEndPoint>().ToList();
            _status = ShareStatus.等待;
        }

        public Task Start()
        {
            lock (_locker)
            {
                if (_started || _disposed)
                    throw new InvalidOperationException();
                _started = true;
                _status = ShareStatus.连接;
                Register();
                OnPropertyChanged(nameof(IsStarted));
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
                        Log.Error(ex);
                    }
                }

                if (soc == null)
                    throw new ApplicationException("Network unreachable.");
                var buf = PacketWriter.Serialize(new
                {
                    data = _key,
                    source = LinkModule.ID,
                });
                await soc.SendAsyncExt(buf.GetBytes());

                lock (_locker)
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
                    _status = ShareStatus.运行;
                    _Receive().ContinueWith(_Finish);
                    return;
                }

                soc?.Dispose();

                lock (_locker)
                {
                    if (_disposed)
                        return;
                    _status = ShareStatus.中断;
                    Dispose();
                }
            });
        }

        private Task _Receive()
        {
            var inf = _batch
                ? ShareModule.AvailableDirectory(_name)
                : (FileSystemInfo)ShareModule.AvailableFile(_name);

            _name = inf.Name;
            _path = inf.FullName;
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(Path));
            // 接收目录
            if (_batch)
                return _ReceiveDir(_path).ContinueWith(task => Log.Error(task.Exception));
            // 接收单个文件
            return _socket.ReceiveFileEx(_path, _length, r => _position += r, _cancel.Token);
        }

        internal async Task _ReceiveDir(string top)
        {
            // 当前目录
            var cur = top;

            while (true)
            {
                var buf = await _socket.ReceiveAsyncExt();
                var rea = new PacketReader(buf);

                switch (rea["type"].Pull<string>())
                {
                    case "end":
                        return;

                    case "dir":
                        // 以根目录为基础重新拼接路径
                        var lst = new List<string>() { top };
                        var dir = rea["path"].PullList<string>();
                        lst.AddRange(dir);
                        cur = System.IO.Path.Combine(lst.ToArray());
                        Directory.CreateDirectory(cur);
                        break;

                    case "file":
                        var key = rea["path"].Pull<string>();
                        var len = rea["length"].Pull<long>();
                        var pth = System.IO.Path.Combine(cur, key);
                        await _socket.ReceiveFileEx(pth, len, r => _position += r, _cancel.Token);
                        break;

                    default:
                        throw new ApplicationException("Batch receive error!");
                }
            }
        }

        private void _Finish(Task task)
        {
            lock (_locker)
            {
                if (_disposed)
                    return;
                var exc = task.Exception;
                _status = (exc == null)
                    ? ShareStatus.成功
                    : ShareStatus.中断;
                Dispose();
            }
        }

        public void Dispose()
        {
            lock (_locker)
            {
                if (_disposed)
                    return;
                if ((_status & ShareStatus.终止) == 0)
                    _status = ShareStatus.取消;

                _cancel.Cancel();
                _socket?.Dispose();
                _socket = null;
                _disposed = true;
                OnPropertyChanged(nameof(IsDisposed));
            }
        }
    }
}

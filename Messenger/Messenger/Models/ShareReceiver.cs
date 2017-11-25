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
using System.Windows;

namespace Messenger.Models
{
    internal class ShareReceiver : ShareBasic
    {
        private readonly object _locker = new object();
        private readonly CancellationTokenSource _cancel = new CancellationTokenSource();

        internal readonly int _id;
        internal readonly Guid _key;
        internal readonly long _length;
        internal readonly bool _batch = false;

        internal bool _started = false;
        internal bool _closed = false;
        internal long _position = 0;
        internal string _name = null;
        internal ShareStatus _status;

        private Socket _socket = null;
        private List<IPEndPoint> _endpoints = null;

        public override long Length => throw new NotImplementedException();

        public override bool IsBatch => throw new NotImplementedException();

        public override bool IsClosed => throw new NotImplementedException();

        public override string Name => throw new NotImplementedException();

        public override string Path => throw new NotImplementedException();

        public override long Position => throw new NotImplementedException();

        public override ShareStatus Status => throw new NotImplementedException();

        protected override int ID => throw new NotImplementedException();

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
                if (_started || _closed)
                    throw new InvalidOperationException();
                _started = true;
                _status = ShareStatus.运行;
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
                    source = Linkers.ID,
                });
                await soc.SendAsyncExt(buf.GetBytes());

                lock (_locker)
                {
                    if (_closed)
                        throw new InvalidOperationException();
                    _socket = soc;
                }
            }

            // 在接收函数退出时设置状态并释放资源
            return _Emit().ContinueWith(t =>
            {
                if (t.Exception == null)
                {
                    _Receive().ContinueWith(_Finish);
                    return;
                }

                soc?.Dispose();

                lock (_locker)
                {
                    if (_closed)
                        return;
                    _status = ShareStatus.中断;
                    Close();
                }
            });
        }

        private Task _Receive()
        {
            // 接收目录
            if (_batch)
                return _ReceiveDir().ContinueWith(task => Log.Error(task.Exception));
            // 接收单个文件
            var inf = ShareModule.AvailableFile(_name);
            _name = inf.Name;
            return _socket.ReceiveFileEx(inf.FullName, _length, r => _position += r, _cancel.Token);
        }

        internal async Task _ReceiveDir()
        {
            // 文件接收根目录
            var inf = ShareModule.AvailableDirectory(_name);
            var top = inf.FullName;
            inf.Create();
            _name = inf.Name;
            // 当前目录
            var cur = inf;

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
                        cur = new DirectoryInfo(System.IO.Path.Combine(lst.ToArray()));
                        cur.Create();
                        break;

                    case "file":
                        var key = rea["path"].Pull<string>();
                        var len = rea["length"].Pull<long>();
                        var pth = System.IO.Path.Combine(cur.FullName, key);
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
                if (_closed)
                    return;
                var exc = task.Exception;
                _status = (exc == null)
                    ? ShareStatus.成功
                    : ShareStatus.中断;
                Close();
            }
        }

        public void Close()
        {
            lock (_locker)
            {
                if (_closed)
                    return;
                if ((_status & ShareStatus.终止) == 0)
                    _status = ShareStatus.取消;
            }

            _cancel.Cancel();
            _socket?.Dispose();
            _socket = null;

            _closed = true;
            Application.Current.Dispatcher.Invoke(() => OnPropertyChanged(nameof(IsClosed)));
        }
    }
}

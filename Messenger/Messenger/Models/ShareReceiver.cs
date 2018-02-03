using Messenger.Extensions;
using Messenger.Modules;
using Mikodev.Logger;
using Mikodev.Network;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Messenger.Models
{
    public sealed class ShareReceiver : ShareBasic, IDisposed
    {
        private readonly object _locker = new object();
        private readonly CancellationTokenSource _cancel = new CancellationTokenSource();

        internal readonly int _id;
        internal readonly Guid _key;
        internal readonly long _length;
        internal readonly bool _batch = false;

        /// <summary>
        /// 原始文件名
        /// </summary>
        internal readonly string _origin;

        internal bool _started = false;
        internal bool _disposed = false;
        internal long _position = 0;
        internal string _name = null;
        internal string _path = null;
        internal ShareStatus _status;

        private readonly IPEndPoint[] _endpoints = null;

        public bool IsStarted => _started;

        public override long Length => _length;

        public override bool IsBatch => _batch;

        public override bool IsDisposed => _disposed;

        public override string Name => _name;

        public override string Path => _path;

        public override long Position => _position;

        public override ShareStatus Status => _status;

        protected override int Id => _id;

        public ShareReceiver(int id, PacketReader reader)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            _id = id;
            var typ = reader["type"].GetValue<string>();
            if (typ == "file")
                _length = reader["length"].GetValue<long>();
            else if (typ == "dir")
                _batch = true;
            else
                throw new ApplicationException("Invalid share type!");

            _key = reader["key"].GetValue<Guid>();
            _origin = reader["name"].GetValue<string>();
            _name = _origin;
            _endpoints = reader["endpoints"].GetArray<IPEndPoint>();
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

            void _SetStatus(ShareStatus status)
            {
                lock (_locker)
                {
                    if (_disposed)
                        return;
                    _status = status;
                }
            }

            async Task _Start()
            {
                var soc = default(Socket);
                var iep = default(IPEndPoint);

                for (int i = 0; i < _endpoints.Length; i++)
                {
                    if (soc != null)
                        break;
                    soc = new Socket(SocketType.Stream, ProtocolType.Tcp);
                    iep = _endpoints[i];

                    try
                    {
                        await soc.ConnectAsyncEx(iep).TimeoutAfter("Share receiver timeout.");
                    }
                    catch (Exception err)
                    {
                        Log.Error(err);
                        soc.Dispose();
                        soc = null;
                    }
                }

                if (soc == null)
                {
                    _SetStatus(ShareStatus.失败);
                    Dispose();
                    return;
                }

                var buf = PacketConvert.Serialize(new
                {
                    path = "share." + (_batch ? "directory" : "file"),
                    data = _key,
                    source = LinkModule.Id,
                    target = _id,
                });

                try
                {
                    soc.SetKeepAlive();
                    await soc.SendAsyncExt(buf);
                    _SetStatus(ShareStatus.运行);
                    await _Receive(soc, _cancel.Token);
                    _SetStatus(ShareStatus.成功);
                    PostModule.Notice(_id, _batch ? "share.dir" : "share.file", _origin);
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                    _SetStatus(ShareStatus.中断);
                    throw;
                }
                finally
                {
                    soc.Dispose();
                    Dispose();
                }
            }

            return Task.Run(_Start);
        }

        internal Task _Receive(Socket socket, CancellationToken token)
        {
            void _UpdateInfo(FileSystemInfo info)
            {
                _name = info.Name;
                _path = info.FullName;
                OnPropertyChanged(nameof(Name));
                OnPropertyChanged(nameof(Path));
            }

            if (_batch)
            {
                var dir = ShareModule.AvailableDirectory(_name);
                _UpdateInfo(dir);
                return socket.ReceiveDirectoryAsyncEx(dir.FullName, r => _position += r, token);
            }

            var inf = ShareModule.AvailableFile(_name);
            _UpdateInfo(inf);
            return socket.ReceiveFileEx(inf.FullName, _length, r => _position += r, token);
        }

        public void Dispose()
        {
            lock (_locker)
            {
                if (_disposed)
                    return;
                var val = _status & ShareStatus.终止;
                if (val == 0)
                    _status = ShareStatus.取消;
                _disposed = true;
            }

            _cancel.Cancel();
            _cancel.Dispose();
            OnPropertyChanged(nameof(IsDisposed));
        }
    }
}

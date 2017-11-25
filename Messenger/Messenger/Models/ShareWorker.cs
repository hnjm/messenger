using Messenger.Extensions;
using Mikodev.Logger;
using Mikodev.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Messenger.Models
{
    public class ShareWorker : ShareBasic, IDisposable
    {
        internal readonly int _id;
        internal readonly object _locker = new object();
        internal readonly Share _source;
        internal readonly Socket _socket;
        internal readonly CancellationTokenSource _cancel = new CancellationTokenSource();
        internal long _position = 0;
        internal bool _started = false;
        internal bool _disposed = false;
        internal ShareStatus _status;

        protected override int ID => _id;

        public override long Length => _source.Length;

        public override bool IsBatch => _source.IsBatch;

        public override bool IsDisposed => _disposed;

        public override string Name => _source._name;

        public override string Path => _source._path;

        public override long Position => _position;

        public override ShareStatus Status => _status;

        public ShareWorker(Share share, int id, Socket socket)
        {
            _id = id;
            _source = share;
            _socket = socket;
            _status = ShareStatus.等待;
        }

        public Task Start()
        {
            lock (_locker)
            {
                if (_started || _disposed)
                    throw new InvalidOperationException();
                _started = true;
                _status = ShareStatus.运行;
                Register();
            }

            if (_source._info is FileInfo inf)
                return _socket.SendFileEx(_source._path, _source._length, r => _position += r, _cancel.Token).ContinueWith(_Finish);
            return _SendDir((DirectoryInfo)_source._info, Enumerable.Empty<string>()).ContinueWith(_Finish);
        }

        internal async Task _SendDir(DirectoryInfo subdir, IEnumerable<string> relative)
        {
            var lst = relative.ToList();
            if (lst.Count > 0)
            {
                // 发送文件夹相对路径
                var wtr = PacketWriter.Serialize(new
                {
                    type = "dir",
                    path = lst,
                });
                var buf = wtr.GetBytes();
                await _socket.SendAsyncExt(buf);
            }

            foreach (var file in subdir.GetFiles())
            {
                var len = file.Length;
                var key = file.Name;
                var wtr = PacketWriter.Serialize(new
                {
                    type = "file",
                    path = key,
                    length = len,
                });
                var buf = wtr.GetBytes();
                await _socket.SendAsyncExt(buf);
                await _socket.SendFileEx(file.FullName, len, r => _position += r, _cancel.Token);
            }

            foreach (var dir in subdir.GetDirectories())
            {
                await _SendDir(dir, relative.Concat(new[] { dir.Name }));
            }

            if (relative.Any() == false)
            {
                var wtr = PacketWriter.Serialize(new
                {
                    type = "end",
                });

                var buf = wtr.GetBytes();
                await _socket.SendAsyncExt(buf);
            }
        }

        internal void _Finish(Task task)
        {
            var exc = task.Exception;
            Log.Error(exc);

            if (_cancel.IsCancellationRequested)
                _status = ShareStatus.取消;
            else if (exc != null)
                _status = ShareStatus.中断;
            else
                _status = ShareStatus.成功;
            Dispose();
        }

        public void Dispose()
        {
            lock (_locker)
            {
                if (_disposed)
                    return;
                _disposed = true;
            }

            Application.Current.Dispatcher.Invoke(() => OnPropertyChanged(nameof(IsDisposed)));
            _cancel.Cancel();
        }
    }
}

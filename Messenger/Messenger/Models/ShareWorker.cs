using Messenger.Extensions;
using Mikodev.Logger;
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Messenger.Models
{
    public sealed class ShareWorker : ShareBasic, IDisposed
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

        protected override int Id => _id;

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
            return _socket.SendDirectoryAsyncEx(_source._path, r => _position += r, _cancel.Token).ContinueWith(_Finish);
        }

        internal void _Finish(Task task)
        {
            var err = task.Exception;
            Log.Error(err);

            if (_cancel.IsCancellationRequested)
                _status = ShareStatus.取消;
            else if (err != null)
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
                _cancel.Cancel();
                _cancel.Dispose();
                _disposed = true;
                OnPropertyChanged(nameof(IsDisposed));
            }
        }
    }
}

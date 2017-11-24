using Messenger.Extensions;
using Mikodev.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Messenger.Models
{
    internal class ShareWorker : IDisposable
    {
        internal readonly int _id;
        internal readonly Share _source;
        internal readonly Socket _socket;
        internal readonly CancellationTokenSource _cancel = new CancellationTokenSource();
        internal long _position = 0;
        internal int _started = 0;
        internal int _disposed = 0;

        /// <summary>
        /// 目标 ID (接收者 ID)
        /// </summary>
        public int Target => _id;

        public bool IsBatch => _source.IsBatch;

        /// <summary>
        /// 已发送的数据量
        /// </summary>
        public long Position => _position;

        public ShareWorker(Share share, int id, Socket socket)
        {
            _id = id;
            _source = share;
            _socket = socket;
        }

        public Task Start()
        {
            if (Volatile.Read(ref _disposed) != 0 || Interlocked.CompareExchange(ref _started, 1, 0) != 0)
                throw new InvalidOperationException();

            if (_source._info is FileInfo inf)
                return _socket.SendFileEx(_source._path, _source._length, r => _position += r, _cancel.Token);
            return _SendDir((DirectoryInfo)_source._info, Enumerable.Empty<string>());
        }

        async Task _SendDir(DirectoryInfo subdir, IEnumerable<string> relative)
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

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
                return;
            _cancel.Cancel();
        }
    }
}

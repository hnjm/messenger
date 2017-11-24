using Messenger.Extensions;
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Messenger.Models
{
    internal class ShareWorker
    {
        internal readonly int _id;
        internal readonly Share _source;
        internal readonly Socket _socket;
        internal readonly CancellationTokenSource _cancel = new CancellationTokenSource();
        internal long _position = 0;

        /// <summary>
        /// 目标 ID (接收者 ID)
        /// </summary>
        public int Target => _id;

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
            if (_source._info is FileInfo inf)
                return _socket.SendFileEx(_source._path, _source._length, r => _position += r, _cancel.Token);
            throw new NotImplementedException();
        }
    }
}

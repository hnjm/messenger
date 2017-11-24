using System;
using System.ComponentModel;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Messenger.Models
{
    internal class Share
    {
        internal static Func<int, Guid, Socket, Task> _backlog;

        internal static void _Register(Share share)
        {
            _backlog += share._Accept;
        }

        /// <summary>
        /// 通知发送者并返回关联任务 (返回值为 null 时表示无可用发送者)
        /// </summary>
        public static Task Notify(int id, Guid key, Socket socket)
        {
            var lst = _backlog?.GetInvocationList();
            if (lst == null)
                return null;
            foreach (var i in lst)
            {
                var fun = (Func<int, Guid, Socket, Task>)i;
                var res = fun.Invoke(id, key, socket);
                if (res == null)
                    continue;
                return res;
            }
            return null;
        }

        internal readonly Guid _key = Guid.NewGuid();
        internal readonly string _name;
        internal readonly string _path;
        internal readonly object _info;
        internal readonly long _length;
        internal readonly BindingList<ShareWorker> _list = new BindingList<ShareWorker>();
        internal int _closed = 0;

        /// <summary>
        /// 是否为批量操作 (目录: 真, 文件: 假)
        /// </summary>
        public bool IsBatch => _info is DirectoryInfo;

        /// <summary>
        /// 文件名或目录名
        /// </summary>
        public string Name => _name;

        /// <summary>
        /// 完整路径
        /// </summary>
        public string Path => _path;

        /// <summary>
        /// 文件长度
        /// </summary>
        public long Length => _length;

        public BindingList<ShareWorker> Workers => _list;

        internal Share(FileSystemInfo info)
        {
            _info = info;
            _name = info.Name;
            _path = info.FullName;
        }

        public Share(FileInfo info) : this((FileSystemInfo)info)
        {
            _length = info.Length;
            _Register(this);
        }

        public Share(DirectoryInfo info) : this((FileSystemInfo)info)
        {
            _Register(this);
        }

        internal Task _Accept(int id, Guid key, Socket socket)
        {
            if (Volatile.Read(ref _closed) != 0 || key != _key)
                return null;
            var obj = new ShareWorker(this, id, socket);
            Application.Current.Dispatcher.Invoke(() => _list.Add(obj));
            return obj.Start();
        }

        public void Close()
        {
            if (Interlocked.CompareExchange(ref _closed, 1, 0) != 0)
                return;
            _backlog -= _Accept;
        }
    }
}

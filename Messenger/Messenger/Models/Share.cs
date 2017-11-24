using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Messenger.Models
{
    internal class Share
    {
        internal readonly Guid _key = Guid.NewGuid();
        internal readonly string _name;
        internal readonly string _path;
        internal readonly object _info;
        internal readonly long _length;

        /// <summary>
        /// 批量操作
        /// </summary>
        public bool IsBatch => _info is DirectoryInfo;

        internal Share(FileSystemInfo info)
        {
            _info = info;
            _name = info.Name;
            _path = info.FullName;
        }

        public Share(FileInfo info) : this((FileSystemInfo)info) => _length = info.Length;

        public Share(DirectoryInfo info) : this((FileSystemInfo)info) { }

        public bool Accept(Guid key, Socket socket)
        {
            if (key != _key)
                return false;
            _Send(socket).ContinueWith(r => { });
            return true;
        }

        internal async Task _Send(Socket socket)
        {

        }
    }
}

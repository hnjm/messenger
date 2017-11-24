using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Messenger.Models
{
    internal class ShareWorker
    {
        internal readonly Share _source;
        internal readonly Socket _socket;

        public ShareWorker(Share share, Socket socket)
        {
            _source = share;
            _socket = socket;
        }
    }
}

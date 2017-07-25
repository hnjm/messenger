using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Messenger.Extensions
{
    public static class Converts
    {
        public static bool GetHost(string str, out string host, out int port)
        {
            if (string.IsNullOrWhiteSpace(str))
                goto fail;
            var idx = str.LastIndexOf(':');
            if (idx < 0)
                goto fail;
            host = str.Substring(0, idx);
            if (string.IsNullOrWhiteSpace(host))
                goto fail;
            if (int.TryParse(str.Substring(idx + 1), out port) == false)
                goto fail;
            return true;
            fail:
            host = null;
            port = 0;
            return false;
        }
    }
}

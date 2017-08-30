using Messenger.Models;
using Messenger.Modules;
using Mikodev.Network;

namespace Messenger.Handles
{
    [Handle("msg")]
    public class Paper : LinkPacket
    {
        [Handle("text")]
        public void Text()
        {
            var txt = Data.Pull<string>();
            Packets.Insert(Source, Target, txt);
        }

        [Handle("image")]
        public void Image()
        {
            var buf = Data.PullList();
            Packets.Insert(Source, Target, buf);
        }
    }
}

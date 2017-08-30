using Messenger.Models;
using Messenger.Modules;
using Mikodev.Network;

namespace Messenger.Handlers
{
    [Handler("msg")]
    public class Paper : LinkPacket
    {
        [Handler("text")]
        public void Text()
        {
            var txt = Data.Pull<string>();
            Packets.Insert(Source, Target, txt);
        }

        [Handler("image")]
        public void Image()
        {
            var buf = Data.PullList();
            Packets.Insert(Source, Target, buf);
        }
    }
}

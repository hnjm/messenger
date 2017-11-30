using Messenger.Models;
using Messenger.Modules;
using Mikodev.Network;

namespace Messenger.Controllers
{
    /// <summary>
    /// 处理共享信息
    /// </summary>
    public class ShareController : LinkPacket
    {
        [Route("share.info")]
        public void Take()
        {
            var rec = new ShareReceiver(Source, Data);
            ShareModule.Register(rec);
            HistoryModule.Insert(Source, Target, rec);
        }
    }
}

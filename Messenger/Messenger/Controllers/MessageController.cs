using Messenger.Models;
using Messenger.Modules;
using Mikodev.Network;

namespace Messenger.Controllers
{
    /// <summary>
    /// 消息处理
    /// </summary>
    [Route("msg")]
    public class MessageController : LinkPacket
    {
        /// <summary>
        /// 文本消息
        /// </summary>
        [Route("text")]
        public void Text()
        {
            var txt = Data.Pull<string>();
            HistoryModule.Insert(Source, Target, txt);
        }

        /// <summary>
        /// 图片消息
        /// </summary>
        [Route("image")]
        public void Image()
        {
            var buf = Data.PullList();
            HistoryModule.Insert(Source, Target, buf);
        }
    }
}

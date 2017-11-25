using Messenger.Models;
using Messenger.Modules;
using Mikodev.Network;

namespace Messenger.Controllers
{
    /// <summary>
    /// 消息处理
    /// </summary>
    [Handle("msg")]
    public class MessageController : LinkPacket
    {
        /// <summary>
        /// 文本消息
        /// </summary>
        [Handle("text")]
        public void Text()
        {
            var txt = Data.Pull<string>();
            HistoryModule.Insert(Source, Target, txt);
        }

        /// <summary>
        /// 图片消息
        /// </summary>
        [Handle("image")]
        public void Image()
        {
            var buf = Data.PullList();
            HistoryModule.Insert(Source, Target, buf);
        }
    }
}

using Messenger.Models;
using Messenger.Modules;
using Mikodev.Network;

namespace Messenger.Handles
{
    /// <summary>
    /// 消息处理
    /// </summary>
    [Handle("msg")]
    public class Paper : LinkPacket
    {
        /// <summary>
        /// 文本消息
        /// </summary>
        [Handle("text")]
        public void Text()
        {
            var txt = Data.Pull<string>();
            Packets.Insert(Source, Target, txt);
        }

        /// <summary>
        /// 图片消息
        /// </summary>
        [Handle("image")]
        public void Image()
        {
            var buf = Data.PullList();
            Packets.Insert(Source, Target, buf);
        }
    }
}

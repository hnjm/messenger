using Messenger.Models;
using Messenger.Modules;
using Mikodev.Network;
using System.Linq;

namespace Messenger.Handles
{
    /// <summary>
    /// 用户信息处理
    /// </summary>
    [Handle("user")]
    public class People : LinkPacket
    {
        /// <summary>
        /// 向发送者返回本机的用户信息
        /// </summary>
        [Handle("request")]
        public void Request()
        {
            Posters.UserProfile(Source);
        }

        /// <summary>
        /// 处理传入的用户信息
        /// </summary>
        [Handle("profile")]
        public void Profile()
        {
            var pro = new Profile()
            {
                ID = Data["id"].Pull<int>(),
                Name = Data["name"].Pull<string>(),
                Text = Data["text"].Pull<string>(),
            };

            var buf = Data["image"].PullList();
            if (buf.Length > 0)
                pro.Image = Caches.SetBuffer(buf, true);
            Profiles.Insert(pro);
        }

        /// <summary>
        /// 处理服务器返回的用户 ID 列表
        /// </summary>
        [Handle("list")]
        public void List()
        {
            var lst = Data.PullList<int>().ToList();
            Profiles.Remove(lst);
        }
    }
}

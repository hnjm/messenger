using Messenger.Models;
using Messenger.Modules;
using Mikodev.Network;
using System.Linq;

namespace Messenger.Controllers
{
    /// <summary>
    /// 处理用户信息
    /// </summary>
    public class ProfileController : LinkPacket
    {
        /// <summary>
        /// 向发送者返回本机的用户信息
        /// </summary>
        [Route("user.request")]
        public void Request()
        {
            PostModule.UserProfile(Source);
        }

        /// <summary>
        /// 处理传入的用户信息
        /// </summary>
        [Route("user.profile")]
        public void Profile()
        {
            var pro = new Profile()
            {
                Id = Data["id"].Pull<int>(),
                Name = Data["name"].Pull<string>(),
                Text = Data["text"].Pull<string>(),
            };

            var buf = Data["image"].PullList();
            if (buf.Length > 0)
                pro.Image = CacheModule.SetBuffer(buf, true);
            ProfileModule.Insert(pro);
        }

        /// <summary>
        /// 处理服务器返回的用户 Id 列表
        /// </summary>
        [Route("user.list")]
        public void List()
        {
            var lst = Data.PullList<int>().ToList();
            ProfileModule.Remove(lst);
        }
    }
}
